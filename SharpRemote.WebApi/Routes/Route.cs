using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpRemote.WebApi.Routes.Parsers;

namespace SharpRemote.WebApi.Routes
{
	/// <summary>
	///     Responsible for determining if a route is matched, extracting
	///     and parsing its parameters.
	/// </summary>
	internal sealed class Route
	{
		private readonly List<ArgumentParser> _arguments;
		private readonly List<RouteToken> _tokens;

		public static Route Create(MethodInfo method)
		{
			var attribute = method.GetCustomAttribute<RouteAttribute>();
			if (attribute == null)
				return null;

			var parameters = method.GetParameters();
			var httpMethod = ExtractHttpMethod(method.Name);
			return new Route(httpMethod, attribute.Template, parameters.Select(x => x.ParameterType));
		}

		private static HttpMethod ExtractHttpMethod(string methodName)
		{
			if (methodName.StartsWith("Get"))
				return HttpMethod.Get;
			if (methodName.StartsWith("Delete"))
				return HttpMethod.Delete;
			if (methodName.StartsWith("Put"))
				return HttpMethod.Put;
			return HttpMethod.Post;
		}

		public Route(HttpMethod method, string route, IEnumerable<Type> parameterTypes, int fromBodyIndex = -1)
		{
			_tokens = RouteToken.Tokenize(route);
			_arguments = new List<ArgumentParser>(parameterTypes.Select(ArgumentParser.Create));
			Method = method;

			var arguments = _tokens.Count(x => x.Type == RouteToken.TokenType.Argument);
			if (arguments != _arguments.Count)
				throw new ArgumentException();

			for (var i = 0; i < _tokens.Count; ++i)
			{
				var token = _tokens[i];
				if (token.Type == RouteToken.TokenType.Argument)
					if (token.ArgumentIndex >= _arguments.Count ||
					    token.ArgumentIndex < 0)
						throw new ArgumentOutOfRangeException(string.Format(
							"Referencing non-existant argument #{0} (there are only {1} arguments)",
							token.ArgumentIndex,
							_arguments.Count));

				if (i < _tokens.Count - 1)
					if (token.Type == RouteToken.TokenType.Argument)
					{
						var argument = _arguments[token.ArgumentIndex];
						if (argument.RequiresTerminator)
						{
							var nextToken = _tokens[i + 1];
							if (nextToken.Type != RouteToken.TokenType.Constant)
								throw new ArgumentException(string.Format("A terminator is required after argument of {0} at index {1}",
									argument.Type, i));
						}
					}
			}
		}

		public HttpMethod Method { get; }

		public bool TryMatch(string route, out object[] values)
		{
			var arguments = new object[_arguments.Count];
			var numArguments = 0;
			var startIndex = 0;
			for (var i = 0; i < _tokens.Count; ++i)
			{
				var token = _tokens[i];
				if (token.Type == RouteToken.TokenType.Argument)
				{
					var argument = _arguments[token.ArgumentIndex];
					int consumed;
					object value;
					if (!argument.TryExtract(route, startIndex, out value, out consumed))
						break;

					arguments[token.ArgumentIndex] = value;
					startIndex += consumed;
					++numArguments;
				}
				else
				{
					if (route.IndexOf(token.Pattern, startIndex) != startIndex)
						break;

					startIndex += token.Pattern.Length;
				}
			}

			if (startIndex < route.Length)
			{
				values = null;
				return false;
			}

			if (numArguments != arguments.Length)
			{
				values = null;
				return false;
			}

			values = arguments;
			return true;
		}
	}
}