using System;
using System.Collections.Generic;
using System.Text;

namespace BusFire.Pipeline
{
	/// <summary>
	/// Represents the result of handling an exception thrown by a command handler
	/// </summary>
	public class CommandExceptionHandlerState<TCommand>
	{
		/// <summary>
		/// Indicates whether the current exception has been handled and the response should be returned.
		/// </summary>
		public bool Handled { get; private set; }

		/// <summary>
		/// The command that failed.
		/// </summary>
		public TCommand Command { get; private set; }

		/// <summary>
		/// Call to indicate whether the current exception should be considered handled and the specified response should be returned.
		/// </summary>
		/// <param name="command">Set the command.</param>
		public void SetHandled(TCommand command)
		{
			Handled = true;
			Command = command;
		}
	}
}
