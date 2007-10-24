#region Apache License, Version 2.0
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion

using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using NMaven.Logging;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop;

namespace NMaven.SharpDevelop.Addin
{
	/// <summary>
	/// Description of CompilerMessageViewLogger.
	/// </summary>
	public class CompilerMessageViewHandler : IHandler
	{
		private Level level;
		
		private CompilerMessageView compilerMessageView;
				
		public CompilerMessageViewHandler()
		{
			this.level = Level.INFO;
		}
		
		public void SetCompilerMessageView(CompilerMessageView compilerMessageView)
		{
			this.compilerMessageView = compilerMessageView;	
		}
		
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void publish(LogRecord record)
		{
			if(record.GetLevel().GetValue() >= level.GetValue())
			{
				compilerMessageView.GetCategory("NMaven Build")
					.AppendText(record.GetMessage());
			}
		}
		
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void SetLevel(Level level)
		{
			this.level = level;
		}
		
		[MethodImpl(MethodImplOptions.Synchronized)]
		public Level GetLevel()
		{
			return level;
		}		
	}
}
