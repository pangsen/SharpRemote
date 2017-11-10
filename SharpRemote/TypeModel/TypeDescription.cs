﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

// ReSharper disable once CheckNamespace
namespace SharpRemote
{
	/// <summary>
	///     Similar to <see cref="Type" /> (in that it describes a particular .NET type), but only
	///     describes its static structure that is important to a <see cref="ISerializer" />.
	/// </summary>
	[DataContract]
	public sealed class TypeDescription
		: ITypeDescription
	{
		/// <summary>
		///     The list of types for which SharpRemote has built-in (de)serialization methods.
		/// </summary>
		private static readonly HashSet<Type> BuiltInTypes;

		private readonly Type _type;
		private readonly Type _byReferenceInterfaceType;
		private TypeDescription _baseType;

		static TypeDescription()
		{
			BuiltInTypes = new HashSet<Type>
			{
				typeof(void),
				typeof(string),
				typeof(decimal)
			};
		}

		/// <summary>
		///     Initializes this object.
		/// </summary>
		public TypeDescription()
		{ }

		/// <summary>
		///     Initializes this object.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="byReferenceInterfaceType"></param>
		private TypeDescription(Type type, Type byReferenceInterfaceType)
		{
			_type = type;
			_byReferenceInterfaceType = byReferenceInterfaceType;
		}

		/// <summary>
		/// The type being described by this object.
		/// </summary>
		public Type Type => _type;

		/// <summary>
		/// The type being described by this object.
		/// </summary>
		public Type ByReferenceInterfaceType => _byReferenceInterfaceType;

		/// <summary>
		///     An id which differentiates this object amongst all others for the same
		///     <see cref="TypeModel" />.
		/// </summary>
		[DataMember]
		public int Id { get; set; }

		/// <summary>
		///     The list of public non-static properties with the <see cref="DataMemberAttribute" />.
		/// </summary>
		[DataMember]
		public PropertyDescription[] Properties { get; set; }

		/// <summary>
		///     The list of public non-static fields with the <see cref="DataMemberAttribute" />.
		/// </summary>
		[DataMember]
		public FieldDescription[] Fields { get; set; }

		/// <summary>
		///     The list of public non-static methods in case this is a <see cref="SharpRemote.SerializationType.ByReference" />
		///     type.
		/// </summary>
		[DataMember]
		public MethodDescription[] Methods { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[DataMember]
		public int BaseTypeId { get; set; }

		/// <summary>
		///     Equivalent of <see cref="System.Type.BaseType" />.
		/// </summary>
		public TypeDescription BaseType
		{
			get { return _baseType; }
			set
			{
				_baseType = value;
				BaseTypeId = value?.Id ?? 0;
			}
		}

		ITypeDescription ITypeDescription.BaseType => _baseType;

		/// <inheritdoc />
		[DataMember]
		public string AssemblyQualifiedName { get; set; }

		/// <inheritdoc />
		[DataMember]
		public SerializationType SerializationType { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsClass { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsEnum { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsInterface { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsValueType { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsSealed { get; set; }

		/// <inheritdoc />
		public bool IsBuiltIn { get; set; }

		/// <inheritdoc />
		[DataMember]
		public bool IsGenericType { get; set; }

		IReadOnlyList<IPropertyDescription> ITypeDescription.Properties => Properties;

		IReadOnlyList<IFieldDescription> ITypeDescription.Fields => Fields;

		IReadOnlyList<IMethodDescription> ITypeDescription.Methods => Methods;

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format("{0}", AssemblyQualifiedName);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="type"></param>
		/// <param name="typesByAssemblyQualifiedName"></param>
		/// <returns></returns>
		public static TypeDescription GetOrCreate(Type type,
		                                          IDictionary<string, TypeDescription> typesByAssemblyQualifiedName)
		{
			TypeDescription description;
			if (!typesByAssemblyQualifiedName.TryGetValue(type.AssemblyQualifiedName, out description))
			{
				return Create(type, typesByAssemblyQualifiedName);
			}
			return description;
		}

		/// <summary>
		///     Creates a new description for the given type.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="typesByAssemblyQualifiedName"></param>
		/// <returns></returns>
		public static TypeDescription Create(Type type, IDictionary<string, TypeDescription> typesByAssemblyQualifiedName)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			var assemblyQualifiedName = type.AssemblyQualifiedName;
			if (assemblyQualifiedName == null)
				throw new ArgumentException("Type.AssemblyQualifiedName should not be null");

			bool builtIn;
			MethodInfo singletonAccessor;
			Type byReferenceInterface;
			var serializerType = GetSerializationType(type, out builtIn, out singletonAccessor, out byReferenceInterface);

			var description = new TypeDescription(type, byReferenceInterface)
			{
				AssemblyQualifiedName = assemblyQualifiedName
			};

			typesByAssemblyQualifiedName.Add(assemblyQualifiedName, description);

			if (IsInterestingBaseType(type.BaseType))
				description.BaseType = Create(type.BaseType, typesByAssemblyQualifiedName);
			description.SerializationType = serializerType;
			description.IsBuiltIn = builtIn;
			description.IsValueType = type.IsValueType;
			description.IsClass = type.IsClass;
			description.IsInterface = type.IsInterface;
			description.IsEnum = type.IsEnum;
			description.IsSealed = type.IsSealed;
			description.IsGenericType = type.IsGenericType;

			switch (description.SerializationType)
			{
				case SerializationType.ByValue:
					// TODO: Throw when ByValue rules are violated
					description.Fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					                         .Where(x => x.GetCustomAttribute<DataMemberAttribute>() != null)
					                         .Select(x => FieldDescription.Create(x, typesByAssemblyQualifiedName)).ToArray();
					description.Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					                             .Where(x => x.GetCustomAttribute<DataMemberAttribute>() != null)
					                             .Select(x => PropertyDescription.Create(x, typesByAssemblyQualifiedName)).ToArray();
					description.Methods = new MethodDescription[0];
					break;

				case SerializationType.ByReference:
					// TODO: Throw when ByReference rules are violated
					description.Fields = new FieldDescription[0];
					description.Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					                             .Select(x => PropertyDescription.Create(x, typesByAssemblyQualifiedName)).ToArray();
					description.Methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
					                          .Where(x => !x.IsSpecialName)
					                          .Select(x => MethodDescription.Create(x, typesByAssemblyQualifiedName)).ToArray();
					break;

				case SerializationType.Singleton:
					description.Fields = new FieldDescription[0];
					description.Properties = new PropertyDescription[0];
					description.Methods = new[] {MethodDescription.Create(singletonAccessor, typesByAssemblyQualifiedName)};
					break;

				case SerializationType.NotSerializable:
					// TODO: Throw proper exception with proper error message
					throw new NotImplementedException();

				default:
					throw new NotImplementedException();
			}


			return description;
		}

		[Pure]
		private static bool IsInterestingBaseType(Type baseType)
		{
			if (baseType == null)
				return false;
			if (baseType == typeof(object))
				return false;
			if (baseType == typeof(ValueType))
				return false;
			return true;
		}

		[Pure]
		private static SerializationType GetSerializationType(Type type,
		                                                      out bool builtIn,
		                                                      out MethodInfo singletonAccessor,
		                                                      out Type byReferenceInterface)
		{
			if (type.IsPrimitive || BuiltInTypes.Contains(type))
			{
				builtIn = true;
				singletonAccessor = null;
				byReferenceInterface = null;
				return SerializationType.ByValue;
			}
			builtIn = false;

			var dataContract = type.GetCustomAttribute<DataContractAttribute>();
			var byReference = type.GetCustomAttribute<ByReferenceAttribute>();
			if (dataContract != null && byReference != null)
				throw new NotImplementedException();

			if (dataContract != null)
			{
				singletonAccessor = null;
				byReferenceInterface = null;
				return SerializationType.ByValue;
			}

			if (byReference != null)
			{
				singletonAccessor = null;
				byReferenceInterface = type.GetInterfaces().FirstOrDefault(x => x.GetCustomAttribute<ByReferenceAttribute>() != null);
				return SerializationType.ByReference;
			}

			var factories = type.GetMethods()
			                    .Where(x => x.GetCustomAttribute<SingletonFactoryMethodAttribute>() != null)
			                    .Concat(type.GetProperties()
			                                .Where(x => x.GetCustomAttribute<SingletonFactoryMethodAttribute>() != null)
			                                .Select(x => x.GetMethod)).ToList();

			if (factories.Count == 0)
				throw new NotImplementedException();

			if (factories.Count > 1)
				throw new ArgumentException(string.Format("The type '{0}' has more than one singleton factory - this is not allowed", type));

			singletonAccessor = factories[0];
			if (singletonAccessor.ReturnType != type)
				throw new ArgumentException(string.Format("The factory method '{0}.{1}' is required to return a value of type '{0}' but doesn't", type, singletonAccessor));

			var @interface = type.GetInterfaces().FirstOrDefault(x => x.GetCustomAttribute<ByReferenceAttribute>() != null);
			if (@interface != null)
			{
				throw new ArgumentException(string.Format(
				                                          "The type '{0}' both has a method marked with the SingletonFactoryMethod attribute and also implements an interface '{1}' which has the ByReference attribute: This is not allowed; they are mutually exclusive",
				                                          type,
				                                          @interface));
			}

			throw new NotImplementedException();
		}
	}
}