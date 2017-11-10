﻿using System.Reflection.Emit;

namespace SharpRemote.CodeGeneration.Serialization.Binary
{
	internal sealed class BinaryReadValueNotNullMethodCompiler
		: AbstractReadValueNotNullMethodCompiler
	{
		public BinaryReadValueNotNullMethodCompiler(CompilationContext context) : base(context)
		{
		}

		protected override void EmitReadByte(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadSByte(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadUShort(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadShort(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadUInt(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadInt(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadULong(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadLong(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadDecimal(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadFloat(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadDouble(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadString(ILGenerator gen)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitBeginReadFieldOrProperty(ILGenerator gen, TypeDescription valueType, string name)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitEndReadFieldOrProperty(ILGenerator gen, TypeDescription valueType, string name)
		{
			throw new System.NotImplementedException();
		}

		protected override void EmitReadHintAndGrainId(ILGenerator generator)
		{
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Callvirt, Methods.ReadByte);
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Callvirt, Methods.ReadLong);
		}
	}
}