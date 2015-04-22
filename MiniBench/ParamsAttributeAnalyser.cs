using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniBench.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MiniBench
{
    internal class ParamsAttributeAnalyser
    {
        private readonly String paramsAttribute = "Params";
        private readonly String paramsWithStepsAttribute = "ParamsWithSteps";

        private readonly Tuple<String, ParamsAttribute, ParamsWithStepsAttribute> emptyParamResult =
           Tuple.Create<String, ParamsAttribute, ParamsWithStepsAttribute>(null, null, null);

        internal Tuple<String, ParamsAttribute, ParamsWithStepsAttribute> GetParamInfo(ClassDeclarationSyntax @class)
        {
            var fields = @class.ChildNodes().OfType<FieldDeclarationSyntax>()
                                .Where(f => f.AttributeLists.SelectMany(al => al.Attributes)
                                    .Any(a => a.Name.ToString() == paramsAttribute || a.Name.ToString() == paramsWithStepsAttribute))
                                .ToList();
            var properties = @class.ChildNodes().OfType<PropertyDeclarationSyntax>()
                                .Where(p => p.AttributeLists.SelectMany(al => al.Attributes)
                                    .Any(a => a.Name.ToString() == paramsAttribute || a.Name.ToString() == paramsWithStepsAttribute))
                                .ToList();

            if (fields.Count + properties.Count > 1)
            {
                var msg = String.Format("Only one field/property can be annotated with [{0}] or [{1}]",
                                        paramsAttribute, paramsWithStepsAttribute);
                throw new InvalidOperationException(msg);
            }

            PrintFieldPropertyDebugInfo(fields, properties);

            if (fields.Count > 0)
                return ProcessFields(fields);

            if (properties.Count > 0)
                return ProcessProperties(properties);

            return emptyParamResult;
        }

        private Tuple<string, ParamsAttribute, ParamsWithStepsAttribute> ProcessProperties(IEnumerable<PropertyDeclarationSyntax> properties)
        {
            foreach (var property in properties)
            {
                var propertyName = property.Identifier.ToString();

                var isPublic = property.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                var isWritable = property.AccessorList.Accessors.Any(m => m.IsKind(SyntaxKind.GetAccessorDeclaration));
                if (isPublic == false || isWritable == false)
                {
                    var msg =
                        String.Format(
                            "Properties annotated with [{0}] or [{1}] must be public and writable, Property: {2} is {3} and {4}",
                            paramsAttribute, paramsWithStepsAttribute, propertyName,
                            String.Join(", ", property.Modifiers),
                            String.Join(", ", property.AccessorList.Accessors));
                    throw new InvalidOperationException(msg);
                }

                var @params = GetParamsAttribute(propertyName, property.AttributeLists);
                var @paramsWithSteps = GetParamsWithStepsAttribute(propertyName, property.AttributeLists);
                return Tuple.Create(propertyName, @params, @paramsWithSteps);
            }
            return emptyParamResult;
        }

        private Tuple<string, ParamsAttribute, ParamsWithStepsAttribute> ProcessFields(IEnumerable<FieldDeclarationSyntax> fields)
        {
            foreach (var field in fields)
            {
                // Throw an error if [ParamsWithSteps] is applied to a field that has multiple variables (i.e. on one line)?
                if (field.Declaration.Variables.Count > 1)
                {
                    var msg =
                        String.Format(
                            "Only one a single field can be annotated with [{0}] or [{1}], Fields: {2}",
                            paramsAttribute, paramsWithStepsAttribute,
                            String.Join(", ", field.Declaration.Variables));
                    throw new InvalidOperationException(msg);
                }

                var variableToUse = field.Declaration.Variables.FirstOrDefault();
                var variableName = variableToUse.Identifier.ToString();

                var isPublic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                if (isPublic == false)
                {
                    var msg =
                        String.Format("Fields annotated with [{0}] or [{1}] must be public, Field: {2} is {3}",
                                      paramsAttribute, paramsWithStepsAttribute,
                                      variableName, String.Join(", ", field.Modifiers));
                    throw new InvalidOperationException(msg);
                }

                var @params = GetParamsAttribute(variableName, field.AttributeLists);
                var @paramsWithSteps = GetParamsWithStepsAttribute(variableName, field.AttributeLists);
                return Tuple.Create(variableName, @params, @paramsWithSteps);
            }
            return emptyParamResult;
        }

        private void PrintFieldPropertyDebugInfo(List<FieldDeclarationSyntax> fields, List<PropertyDeclarationSyntax> properties)
        {
            if (fields.Count > 0)
            {
                var fieldsWithInfo = fields
                    .Select(f =>
                    {
                        return String.Format("{0} {1} {2} -> {3}",
                                             f.Modifiers,
                                             f.Declaration.Type,
                                             String.Join(", ", f.Declaration.Variables.Select(v => v.Identifier)),
                                             String.Join(", ", f.AttributeLists.SelectMany(atr => atr.Attributes)));
                    }).ToList();
                if (fieldsWithInfo.Any())
                    Console.WriteLine("Fields:\n  " + String.Join("\n  ", fieldsWithInfo));
            }

            if (properties.Count > 0)
            {
                var propertiesWithInfo = properties
                    .Select(p =>
                    {
                        return String.Format("{0} {1} {2} {3} -> {4}",
                                             p.Modifiers,
                                             p.Type,
                                             p.Identifier,
                                             p.AccessorList,
                                             String.Join(", ", p.AttributeLists.SelectMany(atr => atr.Attributes)));
                    }).ToList();
                if (propertiesWithInfo.Any())
                    Console.WriteLine("Properties:\n  " + String.Join("\n  ", propertiesWithInfo));
            }
        }

        private ParamsAttribute GetParamsAttribute(string fieldOrPropertyName, SyntaxList<AttributeListSyntax> attributeLists)
        {
            foreach (var attribute in attributeLists.SelectMany(attributeList =>
                            attributeList.Attributes.Where(attribute => attribute.Name.ToString() == paramsAttribute)))
            {
                var arguments = (from argument in attribute.ArgumentList.Arguments
                                 select argument.Expression as LiteralExpressionSyntax
                                     into expression
                                     where expression != null && expression.Token.Value is int
                                     select (int)expression.Token.Value).ToArray();

                // [Params(..)] with zero arguments is not allowed, there must be at least one!!
                if (arguments.Length < 1)
                {
                    var msg = String.Format("The [{0}] attribute must be used with at least 1 value, i.e. \"[{0}(1)]\"", paramsAttribute);
                    throw new InvalidOperationException(msg);
                }

                return new ParamsAttribute(arguments);
            }

            return null;
        }

        private ParamsWithStepsAttribute GetParamsWithStepsAttribute(string fieldOrPropertyName, SyntaxList<AttributeListSyntax> attributeLists)
        {
            foreach (var attribute in attributeLists.SelectMany(attributeList =>
                            attributeList.Attributes.Where(attribute => attribute.Name.ToString() == paramsWithStepsAttribute)))
            {
                var arguments = (from argument in attribute.ArgumentList.Arguments
                                 select argument.Expression as LiteralExpressionSyntax
                                     into expression
                                     where expression != null && expression.Token.Value is int
                                     select (int)expression.Token.Value).ToList();

                const int expectedArgCount = 3;
                if (arguments.Count != expectedArgCount)
                {
                    var rawArgs = String.Join(", ", attribute.ArgumentList.Arguments.Select(
                        arg =>
                        {
                            var literalExpressionSyntax = arg.Expression as LiteralExpressionSyntax;
                            return literalExpressionSyntax != null
                                       ? ((int)(literalExpressionSyntax.Token.Value)).ToString(CultureInfo.InvariantCulture)
                                       : arg.Expression.ToString();
                        }));
                    Console.WriteLine("{0} -> {1} - Wrong number of args, Expected {2}, Got {3}",
                                      fieldOrPropertyName, rawArgs, expectedArgCount, arguments.Count);
                    continue;
                }

                return new ParamsWithStepsAttribute(arguments[0], arguments[1], arguments[2]);
            }

            return null;
        }
    }
}
