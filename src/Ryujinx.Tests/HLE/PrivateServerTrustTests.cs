using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Ssl.SslService;
using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ryujinx.Tests.HLE
{
    /// <summary>
    /// Tests for the two halves of pointing the guest at a private replacement for Nintendo's
    /// servers: letting the SD card's hosts file override the DNS block list, and trusting the
    /// certificate authority that server signs with.
    /// </summary>
    /// <remarks>
    /// The certificate half is what these tests are mostly about, because it is where the tempting
    /// shortcut is. A callback that returns true unconditionally would make a private server work
    /// and would also accept any certificate for any host, on every TLS connection the guest makes,
    /// for as long as the option is on. What is implemented instead <em>adds</em> one CA, and the
    /// tests below are the difference: a name mismatch and an unrelated self-signed certificate
    /// still fail.
    /// </remarks>
    public class PrivateServerTrustTests
    {
        private const string Hostname = "dauth-lp1.ndas.srv.nintendo.net";

        /// <summary>
        /// One instant for every certificate a test makes.
        /// </summary>
        /// <remarks>
        /// Load-bearing. Each helper used to read the clock for itself, so a second ticking over
        /// between making an issuer and making the certificate it signs left the child outliving
        /// its parent by a second -- which X509 refuses, so the test failed roughly one run in
        /// three. Exactly the shape of flake the 1.3.32 changelog entry complains about: a suite
        /// that aborts somewhere different each run.
        /// </remarks>
        private DateTimeOffset _now;

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _now = DateTimeOffset.UtcNow;
            _dir = Path.Combine(Path.GetTempPath(), "ryujinx-private-ca-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        /// <summary>
        /// Makes a self-signed CA, the way a private server's own tooling would.
        /// </summary>
        private X509Certificate2 CreateCa(string name, int validFromDaysAgo = 1)
        {
            using RSA key = RSA.Create(2048);

            CertificateRequest request = new(
                $"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

            // Comfortably wider than anything it signs, at both ends, so no child can fall
            // outside its parent's window.
            return request.CreateSelfSigned(
                _now.AddDays(-validFromDaysAgo).AddDays(-1), _now.AddYears(5));
        }

        /// <summary>
        /// Makes a leaf certificate for a hostname, signed by a CA.
        /// </summary>
        private X509Certificate2 CreateLeaf(X509Certificate2 ca, string hostname)
        {
            using RSA key = RSA.Create(2048);

            CertificateRequest request = new(
                $"CN={hostname}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            SubjectAlternativeNameBuilder sanBuilder = new();
            sanBuilder.AddDnsName(hostname);
            request.CertificateExtensions.Add(sanBuilder.Build());

            byte[] serial = new byte[8];
            RandomNumberGenerator.Fill(serial);

            return request.Create(ca, _now.AddDays(-1), _now.AddDays(30), serial);
        }

        private string WritePem(string fileName, params X509Certificate2[] certificates)
        {
            string path = Path.Combine(_dir, fileName);

            using StreamWriter writer = new(path);

            foreach (X509Certificate2 certificate in certificates)
            {
                writer.WriteLine(certificate.ExportCertificatePem());
            }

            return path;
        }

        /// <summary>
        /// Runs the callback the way <c>SslStream</c> would: with the errors the default validation
        /// produced, and the chain it built against the system store.
        /// </summary>
        private static bool Validate(
            string caBundlePath,
            X509Certificate2 leaf,
            SslPolicyErrors errors,
            X509Certificate2 intermediate = null)
        {
            RemoteCertificateValidationCallback callback = PrivateServerTrust.BuildCallback(caBundlePath);

            Assert.That(callback, Is.Not.Null,
                "no callback was built, so the guest would use ordinary validation");

            using X509Chain chain = new();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

            if (intermediate != null)
            {
                chain.ChainPolicy.ExtraStore.Add(intermediate);
            }

            // Built so the callback receives a chain, as it would in a real handshake. Whether it
            // succeeds is beside the point: the callback rebuilds it against the configured roots.
            chain.Build(leaf);

            return callback(null, leaf, chain, errors);
        }

        [Test]
        public void NoBundleConfiguredLeavesValidationAlone()
        {
            // Null, not a permissive callback. Every connection the guest makes must go through
            // .NET's own validation when this option is off, which is the overwhelmingly common
            // case -- so the option cannot be allowed to change behaviour merely by existing.
            Assert.That(PrivateServerTrust.BuildCallback(null), Is.Null);
            Assert.That(PrivateServerTrust.BuildCallback(string.Empty), Is.Null);
            Assert.That(PrivateServerTrust.BuildCallback("   "), Is.Null);
        }

        [Test]
        public void AMissingOrEmptyBundleLeavesValidationAlone()
        {
            // A path that does not exist must not produce a callback either. Returning one that
            // rejects everything would be indistinguishable from a certificate problem; returning
            // none means the guest gets the ordinary error, and the log line explains why.
            Assert.That(
                PrivateServerTrust.BuildCallback(Path.Combine(_dir, "does-not-exist.pem")),
                Is.Null);

            string empty = Path.Combine(_dir, "empty.pem");
            File.WriteAllText(empty, "# no certificates here\n");

            Assert.That(PrivateServerTrust.BuildCallback(empty), Is.Null);
        }

        [Test]
        public void ACertificateSignedByTheConfiguredCaIsAccepted()
        {
            using X509Certificate2 ca = CreateCa("SwitchNet Local CA");
            using X509Certificate2 leaf = CreateLeaf(ca, Hostname);

            string bundle = WritePem("ca.pem", ca);

            // This is the case the whole feature exists for: a private server serving a Nintendo
            // hostname with a certificate no public CA would ever issue.
            Assert.That(
                Validate(bundle, leaf, SslPolicyErrors.RemoteCertificateChainErrors),
                Is.True,
                "a certificate signed by the configured CA was rejected");
        }

        [Test]
        public void AlreadyValidCertificatesAreStillAccepted()
        {
            using X509Certificate2 ca = CreateCa("SwitchNet Local CA");
            using X509Certificate2 leaf = CreateLeaf(ca, Hostname);

            string bundle = WritePem("ca.pem", ca);

            // The default validation having already passed short-circuits, so a real Nintendo
            // certificate is not re-judged against the private CA.
            Assert.That(Validate(bundle, leaf, SslPolicyErrors.None), Is.True);
        }

        [Test]
        public void AHostnameMismatchIsStillFatal()
        {
            using X509Certificate2 ca = CreateCa("SwitchNet Local CA");
            using X509Certificate2 leaf = CreateLeaf(ca, Hostname);

            string bundle = WritePem("ca.pem", ca);

            // THE test that separates this from disabling verification. The extra CA says the
            // issuer is legitimate; it says nothing about whether the certificate belongs to the
            // host the game asked for. Forgiving this would mean any server the guest was
            // redirected to could present any certificate from that CA.
            Assert.That(
                Validate(bundle, leaf, SslPolicyErrors.RemoteCertificateNameMismatch),
                Is.False,
                "a hostname mismatch was forgiven, which turns a redirect into accept-anything");

            Assert.That(
                Validate(
                    bundle, leaf,
                    SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors),
                Is.False,
                "a hostname mismatch alongside a chain error was forgiven");
        }

        [Test]
        public void ACertificateFromAnUnrelatedCaIsRejected()
        {
            using X509Certificate2 configured = CreateCa("SwitchNet Local CA");
            using X509Certificate2 other = CreateCa("Somebody Else CA");
            using X509Certificate2 leaf = CreateLeaf(other, Hostname);

            string bundle = WritePem("ca.pem", configured);

            // Trusting one CA must not become trusting any self-signed certificate. Without the
            // chain being rebuilt against the configured roots, this is exactly the case that
            // would pass by accident.
            Assert.That(
                Validate(bundle, leaf, SslPolicyErrors.RemoteCertificateChainErrors),
                Is.False,
                "a certificate from an unconfigured CA was accepted");
        }

        [Test]
        public void AMissingCertificateIsRejected()
        {
            using X509Certificate2 ca = CreateCa("SwitchNet Local CA");

            string bundle = WritePem("ca.pem", ca);

            RemoteCertificateValidationCallback callback = PrivateServerTrust.BuildCallback(bundle);

            Assert.That(
                callback(null, null, null, SslPolicyErrors.RemoteCertificateNotAvailable),
                Is.False);
        }

        [Test]
        public void ABundleWithSeveralAuthoritiesTrustsEachOfThem()
        {
            using X509Certificate2 first = CreateCa("First CA");
            using X509Certificate2 second = CreateCa("Second CA");
            using X509Certificate2 fromFirst = CreateLeaf(first, Hostname);
            using X509Certificate2 fromSecond = CreateLeaf(second, Hostname);

            // One file, several PEM blocks: what an operator running two servers, or rotating a
            // CA, actually has.
            string bundle = WritePem("cas.pem", first, second);

            Assert.That(Validate(bundle, fromFirst, SslPolicyErrors.RemoteCertificateChainErrors), Is.True);
            Assert.That(Validate(bundle, fromSecond, SslPolicyErrors.RemoteCertificateChainErrors), Is.True);
        }

        [Test]
        public void AnIntermediateTheServerSentIsUsedToBuildTheChain()
        {
            using X509Certificate2 root = CreateCa("SwitchNet Root CA");

            // An intermediate signed by the root, and a leaf signed by the intermediate: a chain
            // of three, which is what a server that keeps its root offline presents.
            using RSA intermediateKey = RSA.Create(2048);
            CertificateRequest intermediateRequest = new(
                "CN=SwitchNet Issuing CA", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            intermediateRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));

            byte[] serial = new byte[8];
            RandomNumberGenerator.Fill(serial);

            using X509Certificate2 intermediatePublic = intermediateRequest.Create(
                root, _now.AddDays(-1), _now.AddYears(1), serial);
            using X509Certificate2 intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);

            using X509Certificate2 leaf = CreateLeaf(intermediate, Hostname);

            // Only the ROOT is configured, which is the point: the intermediate has to come from
            // what the server sent on the wire.
            string bundle = WritePem("root.pem", root);

            Assert.That(
                Validate(bundle, leaf, SslPolicyErrors.RemoteCertificateChainErrors, intermediatePublic),
                Is.True,
                "a three-certificate chain to the configured root was rejected");
        }

        [Test]
        public void AnExpiredCertificateIsRejectedEvenFromTheConfiguredCa()
        {
            // Valid from further back than the leaf it signs: a leaf cannot start before its
            // issuer does, and this leaf is deliberately in the past.
            using X509Certificate2 ca = CreateCa("SwitchNet Local CA", validFromDaysAgo: 60);

            using RSA key = RSA.Create(2048);
            CertificateRequest request = new(
                $"CN={Hostname}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            SubjectAlternativeNameBuilder sanBuilder = new();
            sanBuilder.AddDnsName(Hostname);
            request.CertificateExtensions.Add(sanBuilder.Build());

            byte[] serial = new byte[8];
            RandomNumberGenerator.Fill(serial);

            using X509Certificate2 expired = request.Create(
                ca, _now.AddDays(-30), _now.AddDays(-1), serial);

            string bundle = WritePem("ca.pem", ca);

            // Adding a CA does not suspend expiry. An operator whose certificate lapsed should see
            // it fail rather than have the emulator paper over it indefinitely.
            Assert.That(
                Validate(bundle, expired, SslPolicyErrors.RemoteCertificateChainErrors),
                Is.False,
                "an expired certificate from the configured CA was accepted");
        }
    }
}
