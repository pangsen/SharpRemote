﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using log4net.Core;
using SharpRemote.Attributes;

namespace SharpRemote.CodeGeneration.Serialization
{
	/// <summary>
	/// </summary>
	public abstract class AbstractReadValueMethodCompiler
		: AbstractMethodCompiler
	{
		private readonly CompilationContext _context;

		/// <summary>
		/// </summary>
		protected AbstractReadValueMethodCompiler(CompilationContext context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));
			if (!typeof(ISerializer2).IsAssignableFrom(context.SerializerType))
				throw new ArgumentException();

			_context = context;
			Method = context.TypeBuilder.DefineMethod("ReadValueNotNull",
			                                          MethodAttributes.Public | MethodAttributes.Static,
			                                          CallingConventions.Standard,
			                                          context.Type,
			                                          new[]
			                                          {
				                                          context.ReaderType,
				                                          context.SerializerType,
				                                          typeof(IRemotingEndPoint)
			                                          });
		}

		/// <inheritdoc />
		public override MethodBuilder Method { get; }

		/// <inheritdoc />
		public override void Compile(AbstractMethodsCompiler methods, ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			var serializationType = _context.TypeDescription.SerializationType;
			switch (serializationType)
			{
				case SerializationType.ByValue:
					if (_context.TypeDescription.IsBuiltIn)
						EmitReadBuiltInType(methodStorage);
					else
						EmitReadByValue(methodStorage);
					break;

				case SerializationType.ByReference:
					EmitReadByReference();
					break;

				case SerializationType.Singleton:
					EmitReadSingleton();
					break;

				case SerializationType.NotSerializable:
					throw new NotImplementedException();

				case SerializationType.Unknown:
					throw new NotImplementedException();

				default:
					throw new InvalidEnumArgumentException("", (int)serializationType, typeof(SerializationType));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitDynamicDispatchReadObject(ILGenerator gen);

		private void EmitReadBuiltInType(ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			var gen = Method.GetILGenerator();
			EmitReadValue(gen, _context.TypeDescription, methodStorage);
			gen.Emit(OpCodes.Ret);
		}

		private void EmitReadSingleton()
		{
			var gen = Method.GetILGenerator();
			var singletonAccessor = _context.TypeDescription.Methods.First();
			gen.Emit(OpCodes.Call, singletonAccessor.Method);
			gen.Emit(OpCodes.Ret);
		}

		private void EmitReadByValue(ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			var gen = Method.GetILGenerator();
			var tmp = gen.DeclareLocal(_context.Type);

			if (_context.Type.IsValueType)
			{
				gen.Emit(OpCodes.Ldloca, tmp);
				gen.Emit(OpCodes.Initobj, _context.Type);
			}
			else
			{
				var ctor = _context.Type.GetConstructor(new Type[0]);
				if (ctor == null)
					throw new ArgumentException(string.Format("Type '{0}' is missing a parameterless constructor", _context.Type));

				gen.Emit(OpCodes.Newobj, ctor);
				gen.Emit(OpCodes.Stloc, tmp);
			}

			EmitBeginRead(gen);

			// tmp.BeforeDeserializationCallback();
			EmitCallBeforeDeserialization(gen, tmp);

			EmitReadFields(gen, tmp, methodStorage);
			EmitReadProperties(gen, tmp, methodStorage);

			// tmp.AfterDeserializationCallback();
			EmitCallAfterSerialization(gen, tmp);

			EmitEndRead(gen);

			// return tmp
			gen.Emit(OpCodes.Ldloc, tmp);
			gen.Emit(OpCodes.Ret);
		}

		private void EmitReadFields(ILGenerator gen,
		                            LocalBuilder local,
		                            ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			foreach (var fieldDescription in _context.TypeDescription.Fields)
				try
				{
					EmitBeginReadField(gen, fieldDescription);
					if (_context.TypeDescription.IsValueType)
					{
						gen.Emit(OpCodes.Ldloca_S, local);
					}
					else
					{
						gen.Emit(OpCodes.Ldloc, local);
					}
					EmitReadValue(gen, fieldDescription.FieldType, methodStorage);
					gen.Emit(OpCodes.Stfld, fieldDescription.Field);
					EmitEndReadField(gen, fieldDescription);
				}
				catch (SerializationException)
				{
					throw;
				}
				catch (Exception e)
				{
					var message = string.Format("There was a problem generating the code to deserialize field '{0} {1}' of type '{2}' ",
					                            fieldDescription.FieldType,
					                            fieldDescription.Name,
					                            _context.Type.FullName
					                           );
					throw new SerializationException(message, e);
				}
		}

		private void EmitReadProperties(ILGenerator gen,
		                                LocalBuilder local,
		                                ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			foreach (var propertyDescription in _context.TypeDescription.Properties)
				try
				{
					EmitBeginReadProperty(gen, propertyDescription);
					if (_context.TypeDescription.IsValueType)
					{
						gen.Emit(OpCodes.Ldloca_S, local);
					}
					else
					{
						gen.Emit(OpCodes.Ldloc, local);
					}
					EmitReadValue(gen, propertyDescription.PropertyType, methodStorage);
					gen.Emit(OpCodes.Call, propertyDescription.SetMethod.Method);
					EmitEndReadProperty(gen, propertyDescription);
				}
				catch (SerializationException)
				{
					throw;
				}
				catch (Exception e)
				{
					var message =
						string.Format("There was a problem generating the code to serialize property '{0} {1}' of type '{2}' ",
						              propertyDescription.PropertyType,
						              propertyDescription.Name,
						              _context.Type.FullName
						             );
					throw new SerializationException(message, e);
				}
		}

		private void EmitReadValue(ILGenerator gen,
		                           ITypeDescription typeDescription,
		                           ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			var type = typeDescription.Type;
			if (type.IsEnum)
				EmitReadEnum(gen, typeDescription);
			else if (type == typeof(byte))
				EmitReadByte(gen);
			else if (type == typeof(sbyte))
				EmitReadSByte(gen);
			else if (type == typeof(ushort))
				EmitReadUInt16(gen);
			else if (type == typeof(short))
				EmitReadInt16(gen);
			else if (type == typeof(uint))
				EmitReadUInt32(gen);
			else if (type == typeof(int))
				EmitReadInt32(gen);
			else if (type == typeof(ulong))
				EmitReadUInt64(gen);
			else if (type == typeof(long))
				EmitReadInt64(gen);
			else if (type == typeof(decimal))
				EmitReadDecimal(gen);
			else if (type == typeof(float))
				EmitReadFloat(gen);
			else if (type == typeof(double))
				EmitReadDouble(gen);
			else if (type == typeof(string))
				EmitReadString(gen);
			else if (type == typeof(DateTime))
				EmitReadDateTime(gen);
			else if (type == typeof(Level))
				EmitReadLevel(gen);
			else if (TypeDescription.IsException(type))
				EmitReadException(gen, type);
			else
				EmitReadDispatched(gen, typeDescription, methodStorage);
		}

		private void EmitReadDispatched(ILGenerator gen,
		                                ITypeDescription typeDescription,
		                                ISerializationMethodStorage<AbstractMethodsCompiler> methodStorage)
		{
			if (typeDescription.IsValueType)
			{
				var methods = methodStorage.GetOrAdd(typeDescription.Type);

				gen.Emit(OpCodes.Ldarg_0);
				gen.Emit(OpCodes.Ldarg_1);
				gen.Emit(OpCodes.Ldarg_2);
				gen.Emit(OpCodes.Call, methods.ReadValueMethod);
			}
			else
			{
				EmitDynamicDispatchReadObject(gen);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="field"></param>
		protected abstract void EmitBeginReadField(ILGenerator gen, IFieldDescription field);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="field"></param>
		protected abstract void EmitEndReadField(ILGenerator gen, IFieldDescription field);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="property"></param>
		protected abstract void EmitBeginReadProperty(ILGenerator gen, IPropertyDescription property);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="property"></param>
		protected abstract void EmitEndReadProperty(ILGenerator gen, IPropertyDescription property);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitBeginRead(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitEndRead(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="typeDescription"></param>
		protected abstract void EmitReadEnum(ILGenerator gen, ITypeDescription typeDescription);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadByte(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadSByte(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadUInt16(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadInt16(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadUInt32(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadInt32(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadUInt64(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadInt64(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadDecimal(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadFloat(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadDouble(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadString(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadDateTime(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		protected abstract void EmitReadLevel(ILGenerator gen);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gen"></param>
		/// <param name="exceptionType"></param>
		protected abstract void EmitReadException(ILGenerator gen, Type exceptionType);

		private void EmitCallBeforeDeserialization(ILGenerator gen, LocalBuilder tmp)
		{
			var method = _context.Type.GetMethods()
			                     .FirstOrDefault(x => x.GetCustomAttribute<BeforeSerializeAttribute>() != null);
			if (method != null)
			{
				gen.Emit(OpCodes.Ldloc, tmp);
				gen.EmitCall(OpCodes.Call, method, optionalParameterTypes: null);
			}
		}

		private void EmitCallAfterSerialization(ILGenerator gen, LocalBuilder tmp)
		{
			var method = _context.Type.GetMethods().FirstOrDefault(x => x.GetCustomAttribute<AfterSerializeAttribute>() != null);
			if (method != null)
			{
				gen.Emit(OpCodes.Ldloc, tmp);
				gen.EmitCall(OpCodes.Call, method, optionalParameterTypes: null);
			}
		}

		private void EmitReadByReference()
		{
			var gen = Method.GetILGenerator();

			var objectRetrieved = gen.DefineLabel();
			var getOrCreateProxy = gen.DefineLabel();

			var hint = gen.DeclareLocal(typeof(ByReferenceHint));
			var id = gen.DeclareLocal(typeof(ulong));

			// hint, id = ReadHintAndGrain() //< defined in inherited class
			EmitReadHintAndGrainId(gen);
			gen.Emit(OpCodes.Stloc, id);
			gen.Emit(OpCodes.Stloc, hint);

			// If hint != RetrieveSubject { goto getOrCreateProxy; }
			gen.Emit(OpCodes.Ldloc, hint);
			gen.Emit(OpCodes.Ldc_I4, (int)ByReferenceHint.RetrieveSubject);
			gen.Emit(OpCodes.Ceq);
			gen.Emit(OpCodes.Brfalse_S, getOrCreateProxy);

			// result = _remotingEndPoint.RetrieveSubject(id)
			var retrieveSubject = typeof(IRemotingEndPoint).GetMethod("RetrieveSubject").MakeGenericMethod(_context.TypeDescription.ByReferenceInterfaceType);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Ldloc, id);
			gen.Emit(OpCodes.Callvirt, retrieveSubject);
			gen.Emit(OpCodes.Br, objectRetrieved);

			gen.MarkLabel(getOrCreateProxy);
			// result = _remotingEndPoint.GetExistingOrCreateNewProxy<T>(serializer.ReadLong());
			var getOrCreateNewProxy = typeof(IRemotingEndPoint)
				.GetMethod("GetExistingOrCreateNewProxy").MakeGenericMethod(_context.TypeDescription.ByReferenceInterfaceType);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Ldloc, id);
			gen.Emit(OpCodes.Callvirt, getOrCreateNewProxy);

			gen.MarkLabel(objectRetrieved);
			gen.Emit(OpCodes.Ret);
		}

		/// <summary>
		///     Shall emit code which reads two values from the given writer and pushes them
		///     onto the evaluation stack in the following order:
		///     1. Shall we create a proxy or servant? => <see cref="ByReferenceHint" />
		///     2. GrainId of the proxy/servant => <see cref="ulong"/>
		/// </summary>
		/// <param name="generator"></param>
		protected abstract void EmitReadHintAndGrainId(ILGenerator generator);
	}
}