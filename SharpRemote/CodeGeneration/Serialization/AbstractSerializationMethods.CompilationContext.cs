﻿using System;
using System.Reflection.Emit;

namespace SharpRemote.CodeGeneration.Serialization
{
	/// <summary>
	/// 
	/// </summary>
	public sealed class CompilationContext
	{
		/// <summary>
		/// 
		/// </summary>
		public TypeDescription TypeDescription { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Type Type => TypeDescription.Type;

		/// <summary>
		/// 
		/// </summary>
		public TypeBuilder TypeBuilder { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Type WriterType { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public Type ReaderType { get; set; }
	}
}