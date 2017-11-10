﻿using System.Reflection.Emit;

namespace SharpRemote.CodeGeneration.Serialization.Xml
{
	internal sealed class XmlReadValueNotNullMethodCompiler
		: AbstractReadValueNotNullMethodCompiler
	{
		public XmlReadValueNotNullMethodCompiler(CompilationContext context)
			: base(context)
		{}

		protected override void EmitReadByte(ILGenerator gen)
		{
		}

		protected override void EmitReadSByte(ILGenerator gen)
		{
		}

		protected override void EmitReadUShort(ILGenerator gen)
		{
		}

		protected override void EmitReadShort(ILGenerator gen)
		{
		}

		protected override void EmitReadUInt(ILGenerator gen)
		{
		}

		protected override void EmitReadInt(ILGenerator gen)
		{
		}

		protected override void EmitReadULong(ILGenerator gen)
		{
		}

		protected override void EmitReadLong(ILGenerator gen)
		{
		}

		protected override void EmitReadDecimal(ILGenerator gen)
		{
		}

		protected override void EmitReadFloat(ILGenerator gen)
		{
		}

		protected override void EmitReadDouble(ILGenerator gen)
		{
		}

		protected override void EmitReadString(ILGenerator gen)
		{
		}

		protected override void EmitBeginReadFieldOrProperty(ILGenerator gen, TypeDescription valueType, string name)
		{
		}

		protected override void EmitEndReadFieldOrProperty(ILGenerator gen, TypeDescription valueType, string name)
		{
		}

		protected override void EmitReadHintAndGrainId(ILGenerator generator)
		{
		}
	}
}