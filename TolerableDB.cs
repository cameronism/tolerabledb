// License: http://www.apache.org/licenses/LICENSE-2.0 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Tolerable
{
	public static class DB
	{
		/// <summary>
		/// Execute command with optional positional parameters
		/// </summary>
		public static int Execute(this IDbConnection dbConnection, string commandText, object[] parameters = null, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			using (var cmd = PrepareCommand(dbConnection, commandText, transaction, null, commandType, parameters))
			{
				return cmd.ExecuteNonQuery();
			}
		}
		
		/// <summary>
		/// Execute command once for each value
		/// </summary>
		public static int Execute<T>(this IDbConnection dbConnection, string commandText, int parameterCount, Action<T, IDbDataParameter[]> setParameterValues, IEnumerable<T> values, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			int total = 0;
			using (var cmd = PrepareCommand(dbConnection, commandText, transaction, null, commandType))
			{
				var parameters = new IDbDataParameter[parameterCount];
				for (int i = 0; i < parameterCount; i++)
				{
					var param = cmd.CreateParameter();
					parameters[i] = param;
					cmd.Parameters.Add(param);
				}
				
				foreach (T value in values)
				{
					setParameterValues(value, parameters);
					total += cmd.ExecuteNonQuery();
				}
			}
			return total;
		}
		
		/// <summary>
		/// Invokes selector once for every row in result
		/// </summary>
		public static IEnumerable<T> Read<T>(this IDbConnection dbConnection, string commandText, object[] parameters, Func<IDataReader, T> selector, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			using (var cmd = PrepareCommand(dbConnection, commandText, transaction, null, commandType, parameters))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						yield return selector(reader);
					}
				}
			}
		}
		
		/// <summary>
		/// Invokes selector once for every row in result
		/// </summary>
		public static IEnumerable<T> Read<T>(this IDbConnection dbConnection, string commandText, Func<IDataReader, T> selector, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			return Read<T>(dbConnection, commandText, null, selector, transaction, commandType);
		}
		
		private static readonly Dictionary<Type, string> _SelectColumns = new Dictionary<Type, string>();
		
		/// prepend SELECT {ctor parameter names} to provided sql
		public static string AddColumnNames(Type type, string sql)
		{
			string toPrepend;
			lock (_SelectColumns)
			{
				if (_SelectColumns.TryGetValue(type, out toPrepend))
				{
					if (toPrepend != null) return toPrepend + sql;
					else throw new ArgumentException("Could not find column names for type", "type");
				}
				
				toPrepend = null;
				var parameters = 
					type.GetConstructors()
						.Select(ctor => ctor.GetParameters())
						.OrderByDescending(ps => ps.Length)
						.FirstOrDefault();
				if (parameters != null && parameters.Length != 0)
				{
					toPrepend = "SELECT " + String.Join(", ", parameters.Select(p => "\"" + p.Name + "\"")) + " ";
				}
				
				_SelectColumns.Add(type, toPrepend);
				if (toPrepend != null) return toPrepend + sql;
				else throw new ArgumentException("Could not find column names for type", "type");
			}
		}
		
		/// <summary>
		/// Select column names corresponding to T's constructor with most parameters. Useful with anonymous types
		/// </summary>
		public static IEnumerable<T> Select<T>(this IDbConnection dbConnection, Func<IDataReader, T> selector, string commandText, object[] parameters = null, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			return Read<T>(dbConnection, AddColumnNames(typeof(T), commandText), parameters, selector, transaction, commandType);
		}
		
		/// <summary>
		/// Invoke action for every row in result
		/// </summary>
		public static void ForEach(this IDbConnection dbConnection, string commandText, Action<IDataReader> action, object[] parameters = null, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			using (var cmd = PrepareCommand(dbConnection, commandText, transaction, null, commandType, parameters))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						action(reader);
					}
				}
			}
		}
		
		/// <summary>
		/// Invoke predicate for every row in result.  Return false to stop iteration.
		/// </summary>
		public static void ForEach(this IDbConnection dbConnection, string commandText, Func<IDataReader, bool> predicate, object[] parameters = null, IDbTransaction transaction = null, CommandType? commandType = null)
		{
			using (var cmd = PrepareCommand(dbConnection, commandText, transaction, null, commandType, parameters))
			{
				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read() && predicate(reader));
				}
			}
		}
		
		private static IDbCommand PrepareCommand(IDbConnection dbConnection, string commandText, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null, object[] parameterValues = null)
		{
			var cmd = dbConnection.CreateCommand();
			if (transaction != null)
				cmd.Transaction = transaction;
			cmd.CommandText = commandText;
			if (commandTimeout.HasValue)
				cmd.CommandTimeout = commandTimeout.Value;
			if (commandType.HasValue)
				cmd.CommandType = commandType.Value;

			if (parameterValues != null)
			{
				foreach (var value in parameterValues)
				{
					var dbParam = cmd.CreateParameter();
					dbParam.Value = value;
					cmd.Parameters.Add(dbParam);
				}
			}
			return cmd;
		}
	}
}
