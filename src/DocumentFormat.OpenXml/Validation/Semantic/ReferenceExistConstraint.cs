﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DocumentFormat.OpenXml.Validation.Semantic
{
    /// <summary>
    /// 3.1 Class for package-level constraint "referenced element must exist".
    /// </summary>
    internal class ReferenceExistConstraint : SemanticConstraint
    {
        private readonly byte _refAttribute;
        private readonly string _partPath;
        private readonly Type _element;
        private readonly string _elementName;
        private readonly byte _attribute;

        public ReferenceExistConstraint(byte refAttribute, string part, Type element, string elementName, byte attribute)
            : base(SemanticValidationLevel.Package)
        {
            Debug.Assert(!string.IsNullOrEmpty(part));

            _refAttribute = refAttribute;
            _partPath = part;
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _elementName = elementName;
            _attribute = attribute;
        }

        public override SemanticValidationLevel StateScope => SemanticValidationLevel.Part;

        public override ValidationErrorInfo Validate(ValidationContext context)
        {
            var element = context.Stack.Current.Element;
            var attribute = element.ParsedState.Attributes[_refAttribute];

            if (!attribute.HasValue || string.IsNullOrEmpty(attribute.Value.InnerText))
            {
                return null;
            }

            var result = GetReferencedAttributes(context);

            if (result.Item.Contains(attribute.Value.InnerText))
            {
                return null;
            }

            return new ValidationErrorInfo()
            {
                Id = "Sem_MissingReferenceElement",
                ErrorType = ValidationErrorType.Semantic,
                Node = element,
                RelatedPart = result.Part,
                RelatedNode = null,
                Description = SR.Format(
                    ValidationResources.Sem_MissingReferenceElement,
                    _elementName,
                    element.LocalName,
                    GetAttributeQualifiedName(element, _refAttribute),
                    result.Part == null ? _partPath : result.Part.PackagePart.Uri.ToString(),
                    attribute.Value.InnerText),
            };
        }

        private PartHolder<HashSet<string>> GetReferencedAttributes(ValidationContext context)
        {
            var referencedAttributes = new HashSet<string>(StringComparer.Ordinal);
            var part = GetReferencedPart(context, _partPath);

            void ElementTraverse(ValidationContext ctx)
            {
                var element = ctx.Stack.Current.Element;

                Debug.Assert(element != null);

                if (element.GetType() == _element)
                {
                    var attribute = element.ParsedState.Attributes[_attribute];

                    //Attributes whose value is empty string or null don't need to be cached.
                    if (attribute.HasValue && !string.IsNullOrEmpty(attribute.Value.InnerText))
                    {
                        referencedAttributes.Add(attribute.Value.InnerText);
                    }
                }
            }

            if (part != null)
            {
                var partContext = new ValidationContext(context);

                using (partContext.Stack.Push(context.Stack.Current.Package, part, part.RootElement))
                {
                    ValidationTraverser.ValidatingTraverse(partContext, ElementTraverse, null);
                }
            }

            return new PartHolder<HashSet<string>>(referencedAttributes, part);
        }
    }
}
