#region Licence
/*
The MIT License (MIT)

Copyright (c) 2015 Babbacombe Computers Ltd

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace XmlDocRinse {

    /// <summary>
    /// Removes non-public items from the XML documentation
    /// produced by Visual Studio.
    /// </summary>

    class Program {
        static void Main(string[] args) {
            if (args.Length == 0) {
                Console.WriteLine("Usage: XmlDocRinse <assembly path> [<xml doc path>]");
                return;
            }

            string assPath = args[0];
            if (!File.Exists(assPath)) {
                Console.WriteLine("Assembly '{0}' not found", assPath);
                return;
            }
            string xmlPath = args.Length > 1
                ? args[1]
                : Path.ChangeExtension(assPath, "xml");
            if (!File.Exists(xmlPath)) {
                Console.WriteLine("XML Doc file '{0}' not found", xmlPath);
                return;
            }
            File.Copy(xmlPath, xmlPath + ".backup", true);

            var assembly = Assembly.LoadFrom(assPath);
            var stringSet = new XmlDocumentationStringSet(assembly);

            var el = XElement.Load(xmlPath);
            foreach (var member in el.Descendants("member").ToList()) {
                var attr = member.Attribute("name");
                if (attr == null) {
                    continue;
                }
                if (!stringSet.Contains(attr.Value)) {
                    member.Remove();
                }
            }
            el.Save(xmlPath);
            Console.WriteLine("Rinsed '{0}'", xmlPath);
        }
    }

    class XmlDocumentationStringSet : IEnumerable<string> {
        private HashSet<string> stringSet = new HashSet<string>(StringComparer.Ordinal);

        public XmlDocumentationStringSet(Assembly assembly) {
            AddRange(assembly.GetExportedTypes());
        }

        public bool Contains(string name) {
            return stringSet.Contains(name);
        }

        private void AddRange(IEnumerable<Type> types) {
            foreach (var type in types) {
                Add(type);
            }
        }

        private void Add(Type type) {
            // Public API only
            if (!type.IsVisible) {
                return;
            }
            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var member in members) {
                Add(type, member);
            }
        }

        private StringBuilder sb = new StringBuilder();

        private void Add(Type type, MemberInfo member) {
            Type nestedType = null;

            sb.Length = 0;

            switch (member.MemberType) {
                case MemberTypes.Constructor:
                    sb.Append("M:");
                    AppendConstructor(sb, (ConstructorInfo)member);
                    break;
                case MemberTypes.Event:
                    sb.Append("E:");
                    AppendEvent(sb, (EventInfo)member);
                    break;
                case MemberTypes.Field:
                    sb.Append("F:");
                    AppendField(sb, (FieldInfo)member);
                    break;
                case MemberTypes.Method:
                    sb.Append("M:");
                    AppendMethod(sb, (MethodInfo)member);
                    break;
                case MemberTypes.NestedType:
                    nestedType = (Type)member;
                    if (IsVisible(nestedType)) {
                        sb.Append("T:");
                        AppendNestedType(sb, (Type)member);
                    }
                    break;
                case MemberTypes.Property:
                    sb.Append("P:");
                    AppendProperty(sb, (PropertyInfo)member);
                    break;
            }

            if (sb.Length > 0) {
                stringSet.Add(sb.ToString());
            }

            if (nestedType != null) {
                Add(nestedType);
            }
        }

        private bool IsVisible(Type nestedType) {
            return nestedType.IsVisible;
        }

        private void AppendProperty(StringBuilder sb, PropertyInfo propertyInfo) {
            if (!IsVisible(propertyInfo)) {
                sb.Length = 0;
                return;
            }
            AppendType(sb, propertyInfo.DeclaringType);
            sb.Append('.').Append(propertyInfo.Name);
        }

        private bool IsVisible(PropertyInfo propertyInfo) {
            var getter = propertyInfo.GetGetMethod();
            var setter = propertyInfo.GetSetMethod();
            return (getter != null && IsVisible(getter)) || (setter != null && IsVisible(setter));
        }

        private void AppendNestedType(StringBuilder sb, Type type) {
            AppendType(sb, type.DeclaringType);
        }

        private void AppendMethod(StringBuilder sb, MethodInfo methodInfo) {
            if (!IsVisible(methodInfo) || (methodInfo.IsHideBySig && methodInfo.IsSpecialName)) {
                sb.Length = 0;
                return;
            }
            AppendType(sb, methodInfo.DeclaringType);
            sb.Append('.').Append(methodInfo.Name);
            AppendParameters(sb, methodInfo.GetParameters());
        }

        private bool IsVisible(MethodInfo methodInfo) {
            return methodInfo.IsFamily || methodInfo.IsPublic;
        }

        private void AppendParameters(StringBuilder sb, ParameterInfo[] parameterInfo) {
            if (parameterInfo.Length == 0) {
                return;
            }
            sb.Append('(');
            for (int i = 0; i < parameterInfo.Length; i++) {
                if (i > 0) {
                    sb.Append(',');
                }
                var p = parameterInfo[i];
                AppendType(sb, p.ParameterType);
            }
            sb.Append(')');
        }

        private void AppendField(StringBuilder sb, FieldInfo fieldInfo) {
            if (!IsVisible(fieldInfo)) {
                sb.Length = 0;
                return;
            }
            AppendType(sb, fieldInfo.DeclaringType);
            sb.Append('.').Append(fieldInfo.Name);
        }

        private bool IsVisible(FieldInfo fieldInfo) {
            return fieldInfo.IsFamily || fieldInfo.IsPublic;
        }

        private void AppendEvent(StringBuilder sb, EventInfo eventInfo) {
            if (!IsVisible(eventInfo)) {
                sb.Length = 0;
                return;
            }
            AppendType(sb, eventInfo.DeclaringType);
            sb.Append('.').Append(eventInfo.Name);
        }

        private bool IsVisible(EventInfo eventInfo) {
            return eventInfo.GetAddMethod(false) != null;
        }

        private void AppendConstructor(StringBuilder sb, ConstructorInfo constructorInfo) {
            if (!IsVisible(constructorInfo)) {
                sb.Length = 0;
                return;
            }
            AppendType(sb, constructorInfo.DeclaringType);
            sb.Append('.').Append("#ctor");
            AppendParameters(sb, constructorInfo.GetParameters());
        }

        private bool IsVisible(ConstructorInfo constructorInfo) {
            return constructorInfo.IsFamily || constructorInfo.IsPublic;
        }

        private void AppendType(StringBuilder sb, Type type) {
            if (type.DeclaringType != null) {
                AppendType(sb, type.DeclaringType);
                sb.Append('.');
            } else if (!string.IsNullOrEmpty(type.Namespace)) {
                sb.Append(type.Namespace);
                sb.Append('.');
            }
            sb.Append(type.Name);
            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                // Remove "`1" suffix from type name
                while (char.IsDigit(sb[sb.Length - 1]))
                    sb.Length--;
                sb.Length--;
                {
                    var args = type.GetGenericArguments();
                    sb.Append('{');
                    for (int i = 0; i < args.Length; i++) {
                        if (i > 0) {
                            sb.Append(',');
                        }
                        AppendType(sb, args[i]);
                    }
                    sb.Append('}');
                }
            }
        }

        public IEnumerator<string> GetEnumerator() {
            return stringSet.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

}
