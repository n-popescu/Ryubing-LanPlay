using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Collections.Immutable;
using System.Threading;

namespace Ryujinx.HLE.Generators
{
    [Generator]
    public class IpcCommandGenerator : IIncrementalGenerator
    {
        private sealed class CommandData : IEquatable<CommandData>
        {
            public required string Namespace { get; init; }
            public required string TypeName { get; init; }
            public required string MethodName { get; init; }
            public required ImmutableArray<int> CommandIds { get; init; }

            public bool Equals(CommandData other)
            {
                return Namespace == other.Namespace && TypeName == other.TypeName && MethodName == other.MethodName && CommandIds.SequenceEqual(other.CommandIds);
            }

            public override bool Equals(object obj)
                => obj is CommandData other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Namespace != null ? Namespace.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (TypeName != null ? TypeName.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (MethodName != null ? MethodName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private sealed class ServiceData : IEquatable<ServiceData>
        {
            public required string Namespace { get; init; }
            public required string TypeName { get; init; }
            public required ImmutableArray<CommandData> CmifCommands { get; init; }
            public required ImmutableArray<CommandData> TipcCommands { get; init; }


            public bool Equals(ServiceData other)
            {
                return Namespace == other.Namespace && TypeName == other.TypeName && CmifCommands.SequenceEqual(other.CmifCommands) && TipcCommands.SequenceEqual(other.TipcCommands);
            }

            public override bool Equals(object obj)
            {
                return obj is ServiceData other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Namespace != null ? Namespace.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (TypeName != null ? TypeName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            Func<SyntaxNode, CancellationToken, bool> predicate = (node, _) => node is MethodDeclarationSyntax;
            Func<GeneratorAttributeSyntaxContext, CancellationToken, CommandData> transform = (ctx, _) =>
            {
                IMethodSymbol target = (IMethodSymbol)ctx.TargetSymbol;
                return new CommandData
                {
                    Namespace = target.ContainingType.ContainingNamespace?.ToDisplayString(),
                    TypeName = target.ContainingType.Name,
                    MethodName = target.Name,
                    CommandIds = ctx.Attributes.Select(attr => (int)attr.ConstructorArguments[0].Value!).ToImmutableArray(),
                };
            };
            IncrementalValuesProvider<CommandData> cmifCommands =
                context.SyntaxProvider.ForAttributeWithMetadataName("Ryujinx.HLE.HOS.Services.CommandCmifAttribute",
                    predicate,
                    transform
                );
            IncrementalValuesProvider<CommandData> tipcCommands =
                context.SyntaxProvider.ForAttributeWithMetadataName("Ryujinx.HLE.HOS.Services.CommandTipcAttribute",
                    predicate,
                    transform
                );

            IncrementalValueProvider<(ImmutableArray<CommandData> Left, ImmutableArray<CommandData> Right)> allCommands = 
                cmifCommands.Collect().Combine(tipcCommands.Collect());

            IncrementalValuesProvider<ServiceData> types = allCommands.SelectMany((commands, _) =>
            {
                ILookup<(string Namespace, string TypeName), CommandData> cmif = commands.Left.ToLookup(c => (c.Namespace, c.TypeName));
                ILookup<(string Namespace, string TypeName), CommandData> tipc = commands.Right.ToLookup(c => (c.Namespace, c.TypeName));

                ImmutableArray<ServiceData>.Builder builder = ImmutableArray.CreateBuilder<ServiceData>();

                foreach ((string Namespace, string TypeName) type in cmif.Select(c => c.Key).Union(tipc.Select(t => t.Key)))
                {
                    builder.Add(new ServiceData
                    {
                        Namespace = type.Namespace,
                        TypeName = type.TypeName,
                        CmifCommands = cmif.Contains(type) ? cmif[type].ToImmutableArray() : [],
                        TipcCommands = tipc.Contains(type) ? tipc[type].ToImmutableArray() : [],
                    });
                }

                return builder.DrainToImmutable();
            });
            
            context.RegisterSourceOutput(types, (ctx, data) =>
            {
                CodeGenerator generator = new();
                
                generator.AppendLine("using Ryujinx.HLE.HOS;");
                generator.AppendLine("using RC = global::Ryujinx.HLE.HOS.ResultCode;");
                
                generator.EnterScope($"namespace {data.Namespace}");
                generator.EnterScope($"partial class {data.TypeName}");

                if (!data.CmifCommands.IsEmpty)
                {
                    GenerateCommandMethod("Cmif", data.CmifCommands);
                }

                if (!data.TipcCommands.IsEmpty)
                {
                    GenerateCommandMethod("Tipc", data.TipcCommands);
                }

                generator.LeaveScope();
                generator.LeaveScope();
                
                ctx.AddSource($"{data.Namespace}.{data.TypeName}.g.cs", generator.ToString());

                void GenerateCommandMethod(string commandType, ImmutableArray<CommandData> commands)
                {
                    generator.EnterScope($"protected override RC Invoke{commandType}Method(int id, ServiceCtx context)");
                    generator.EnterScope("switch (id)");
                    foreach (CommandData command in commands)
                    {
                        generator.AppendLine($"case {string.Join(" or ", command.CommandIds)}:");
                        generator.IncreaseIndentation();
                        generator.AppendLine($"LogInvoke(\"{command.MethodName}\");");
                        generator.AppendLine($"return (RC){command.MethodName}(context);");
                        generator.DecreaseIndentation();
                    }
                    generator.AppendLine($"default: return base.Invoke{commandType}Method(id, context);");
                    generator.LeaveScope();
                    generator.LeaveScope();
                
                    generator.EnterScope($"public override int {commandType}CommandIdByMethodName(string name)");
                    generator.EnterScope("return name switch");
                    foreach (CommandData command in commands)
                    {
                        // just return the first command with this name
                        generator.AppendLine($"\"{command.MethodName}\" => {command.CommandIds[0]},");
                    }
                    generator.AppendLine($"_ => base.{commandType}CommandIdByMethodName(name),");
                    generator.LeaveScope(";");
                    generator.LeaveScope();
                }
            });
        }
    }
}
