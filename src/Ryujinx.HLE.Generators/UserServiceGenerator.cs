using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.HLE.Generators
{
    [Generator]
    public sealed class UserServiceGenerator : IIncrementalGenerator
    {
        private sealed class ServiceData : IEquatable<ServiceData>
        {
            public required string FullName { get; init; }
            public required IReadOnlyList<(string ServiceName, string ParameterValue)> Instances { get; init; }

            public override bool Equals(object obj)
                => obj is ServiceData data && Equals(data);

            public bool Equals(ServiceData other)
            {
                return this.FullName == other.FullName && this.Instances.SequenceEqual(other.Instances);
            }

            public override int GetHashCode() => FullName.GetHashCode();
        }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<ServiceData> pipeline = context.SyntaxProvider.ForAttributeWithMetadataName("Ryujinx.HLE.HOS.Services.ServiceAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax decl && !decl.Modifiers.Any(SyntaxKind.AbstractKeyword) && !decl.Modifiers.Any(SyntaxKind.PrivateKeyword),
                transform: (ctx, _) =>
                {
                    INamedTypeSymbol target = (INamedTypeSymbol)ctx.TargetSymbol;
                    IEnumerable<(string, string param)> instances = ctx.Attributes.Select(attr =>
                    {
                        string param = attr.ConstructorArguments is [_, { IsNull: false } arg] ? arg.ToCSharpString() : null;
                        return ((string)attr.ConstructorArguments[0].Value, param);
                    });
                    return new ServiceData
                    {
                        FullName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        Instances = instances.ToList(),
                    };
                }
            );
            
            context.RegisterSourceOutput(pipeline.Collect(),
                (ctx, data) =>
                {
                    CodeGenerator generator = new();
                    
                    generator.AppendLine("#nullable enable");
                    generator.AppendLine("using System;");
                    generator.EnterScope("namespace Ryujinx.HLE.HOS.Services.Sm");
                    generator.EnterScope("partial class IUserInterface");

                    generator.EnterScope("public IpcService? GetServiceInstance(string name, ServiceCtx context)");
                    
                    generator.EnterScope("return name switch");

                    foreach (ServiceData serviceImpl in data)
                    {
                        foreach ((string ServiceName, string ParameterValue) instance in serviceImpl.Instances)
                        {
                            if (instance.ParameterValue == null)
                            {
                                generator.AppendLine($"\"{instance.ServiceName}\" => new {serviceImpl.FullName}(context),");
                            }
                            else
                            {
                                generator.AppendLine($"\"{instance.ServiceName}\" => new {serviceImpl.FullName}(context, {instance.ParameterValue}),");
                            }
                        }
                    }
                    
                    generator.AppendLine("_ => null,");
                    
                    generator.LeaveScope(";");
                    
                    generator.LeaveScope();

                    generator.LeaveScope();
                    generator.LeaveScope();
                    
                    generator.AppendLine("#nullable disable");
                    
                    ctx.AddSource("IUserInterface.g.cs", generator.ToString());
                });
        }
    }
}
