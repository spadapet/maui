// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Media;

namespace Microsoft.Maui
{
	public static class VisualDiagnostics
	{
		static ConditionalWeakTable<object, SourceInfo> sourceInfos = new ConditionalWeakTable<object, SourceInfo>();
		static Lazy<bool> isVisualDiagnosticsEnvVarSet = new Lazy<bool>(() => Environment.GetEnvironmentVariable("ENABLE_XAML_DIAGNOSTICS_SOURCE_INFO") is { } value && value == "1");

		static internal bool IsEnabled => DebuggerHelper.DebuggerIsAttached || isVisualDiagnosticsEnvVarSet.Value;

		/// <include file="../../docs/Microsoft.Maui/VisualDiagnostics.xml" path="//Member[@MemberName='RegisterSourceInfo']/Docs/*" />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void RegisterSourceInfo(object target, Uri uri, int lineNumber, int linePosition)
		{
#if !NETSTANDARD2_0
			if (target != null && VisualDiagnostics.IsEnabled)
				sourceInfos.AddOrUpdate(target, new SourceInfo(uri, lineNumber, linePosition));
#else
			if (target != null && VisualDiagnostics.IsEnabled)
			{
				if (sourceInfos.TryGetValue(target, out _))
					sourceInfos.Remove(target);
				sourceInfos.Add(target, new SourceInfo(uri, lineNumber, linePosition));
			}
#endif
		}

		/// <include file="../../docs/Microsoft.Maui/VisualDiagnostics.xml" path="//Member[@MemberName='GetXamlSourceInfo']/Docs/*" />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static SourceInfo? GetSourceInfo(object obj) =>
			sourceInfos.TryGetValue(obj, out var sourceInfo) ? sourceInfo : null;

		public static void OnChildAdded(IVisualTreeElement parent, IVisualTreeElement child)
		{
			if (!VisualDiagnostics.IsEnabled)
				return;

			if (child is null)
				return;

			var index = parent?.GetVisualChildren().IndexOf(child) ?? -1;

			OnChildAdded(parent, child, index);
		}

		public static void OnChildAdded(IVisualTreeElement? parent, IVisualTreeElement child, int newLogicalIndex)
		{
			if (!VisualDiagnostics.IsEnabled)
				return;

			if (child is null)
				return;

			OnVisualTreeChanged(new VisualTreeChangeEventArgs(parent, child, newLogicalIndex, VisualTreeChangeType.Add));
		}

		public static void OnChildRemoved(IVisualTreeElement parent, IVisualTreeElement child, int oldLogicalIndex)
		{
			if (!VisualDiagnostics.IsEnabled)
				return;

			OnVisualTreeChanged(new VisualTreeChangeEventArgs(parent, child, oldLogicalIndex, VisualTreeChangeType.Remove));
		}

		public static event EventHandler<VisualTreeChangeEventArgs>? VisualTreeChanged;

		static void OnVisualTreeChanged(VisualTreeChangeEventArgs e)
		{
			VisualTreeChanged?.Invoke(e.Parent, e);
		}

		public static async Task<byte[]?> CaptureAsPngAsync(IView view)
		{
			var result = await view.CaptureAsync();
			return await ScreenshotResultToArray(result, ScreenshotFormat.Png, 100);
		}

		public static async Task<byte[]?> CaptureAsJpegAsync(IView view, int quality = 80)
		{
			var result = await view.CaptureAsync();
			return await ScreenshotResultToArray(result, ScreenshotFormat.Jpeg, quality);
		}

		public static async Task<byte[]?> CaptureAsPngAsync(IWindow window)
		{
			var result = await window.CaptureAsync();
			return await ScreenshotResultToArray(result, ScreenshotFormat.Png, 100);
		}

		public static async Task<byte[]?> CaptureAsJpegAsync(IWindow window, int quality = 80)
		{
			var result = await window.CaptureAsync();
			return await ScreenshotResultToArray(result, ScreenshotFormat.Jpeg, quality);
		}

		static async Task<byte[]?> ScreenshotResultToArray(IScreenshotResult? result, ScreenshotFormat format, int quality)
		{
			if (result is null)
				return null;

			using var ms = new MemoryStream();
			await result.CopyToAsync(ms, format, quality);

			return ms.ToArray();
		}

		[ThreadStatic]
		private static int blockCallStack;

		internal static IDisposable BlockSourceInfoFromCallStack()
		{
			VisualDiagnostics.blockCallStack++;

			return new ActionDisposable(() => VisualDiagnostics.blockCallStack--);
		}

		internal static void RegisterSourceInfoFromCallStack(object target)
		{
			if (target?.GetType() is not Type targetType || !VisualDiagnostics.IsEnabled)
				return;

			switch (targetType.Name)
			{
				case "Border":
					VisualDiagnostics.RegisterSourceInfo(target, new Uri(@"MainPage.xaml.cs;assembly=Maui.Controls.Sample.Sandbox", UriKind.Relative), 23, 3);
					return;

				case "Button":
					VisualDiagnostics.RegisterSourceInfo(target, new Uri(@"MainPage.xaml.cs;assembly=Maui.Controls.Sample.Sandbox", UriKind.Relative), 13, 3);
					return;

				case "Grid":
					VisualDiagnostics.RegisterSourceInfo(target, new Uri(@"MainPage.xaml.cs;assembly=Maui.Controls.Sample.Sandbox", UriKind.Relative), 28, 3);
					return;
			}

			// Debug.WriteLine($"*** CREATING:{target.GetType().FullName}");

			StackTrace stackTrace = new();
			bool foundConstructor = false;

			for (int i = 0; i < stackTrace.FrameCount; i++)
			{
				if (stackTrace.GetFrame(i) is StackFrame frame &&
					frame.GetMethod() is MethodBase method &&
					method.DeclaringType is Type methodType &&
					methodType.Assembly is Assembly methodAssembly &&
					methodAssembly.FullName is string methodAssemblyFullName)
				{
					// The frame after the constructor is where the target object was created
					if (foundConstructor)
					{
						if (methodAssemblyFullName.StartsWith("Microsoft.Maui,", StringComparison.OrdinalIgnoreCase) ||
							methodAssemblyFullName.StartsWith("Microsoft.Maui.", StringComparison.OrdinalIgnoreCase))
						{
							// Ignore elements created by MAUI itself
							break;
						}

						if (methodAssemblyFullName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
						{
							// Ignore stack frames in System (like when Activator is used to construct an element)
							continue;
						}
#if !NETSTANDARD2_0
						int commaIndex = methodAssemblyFullName.IndexOf(',', StringComparison.Ordinal);
#else
						int commaIndex = methodAssemblyFullName.IndexOf(',');
#endif
						string assemblyName = (commaIndex > 0) ? methodAssemblyFullName.Substring(0, commaIndex) : methodAssemblyFullName;
						int offset = frame.HasILOffset() ? frame.GetILOffset() : 0;

						VisualDiagnostics.RegisterSourceInfo(target, new Uri($"code:{methodType.FullName}.{method.Name}, {assemblyName}, {methodAssembly.Location}"), offset + 1, 1);

						break;
					}
					
					foundConstructor = method.IsConstructor && methodType == targetType;
				}
			}
		}
	}
}
