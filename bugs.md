2025-05-17 16:17:52.435 +01:00 [INF] ... IsFullScreenMode changed: true
2025-05-17 16:17:53.368 +01:00 [INF] Applied full-screen settings on screen: \\.\DISPLAY2
2025-05-17 16:17:53.375 +01:00 [INF] MainViewModel: RefreshLayout called - forceRebuild=false
2025-05-17 16:17:53.375 +01:00 [INF] .... HyperspaceOverlay forced to hidden state
2025-05-17 16:17:53.376 +01:00 [INF] .... Connecting HyperspaceOverlay to GameState
2025-05-17 16:17:53.377 +01:00 [INF] .... HyperspaceOverlay now HIDDEN
2025-05-17 16:17:53.379 +01:00 [INF] CarrierJumpOverlay.UpdateVisibility: ShowCarrierJumpOverlay=false, JumpInProgress=true, CountdownSeconds=376, IsOnFleetCarrier=false, JumpArrived=false
2025-05-17 16:17:53.379 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-17 16:17:53.379 +01:00 [INF] Resetting stale carrier jump state - JumpInProgress=true, OnCarrier=false, JumpArrived=false
2025-05-17 16:17:53.380 +01:00 [INF] .... FleetCarrierJumpInProgress changed to false - Stack trace:    at System.Environment.get_StackTrace()
   at EliteInfoPanel.Core.GameStateService.set_FleetCarrierJumpInProgress(Boolean value) in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 372
   at EliteInfoPanel.Core.GameStateService.ResetFleetCarrierJumpState() in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 561
   at EliteInfoPanel.MainWindow.Window_Loaded(Object sender, RoutedEventArgs e) in C:\Users\jimmy\source\repos\EliteInfoPanel\MainWindow.xaml.cs:line 556
   at System.Windows.EventRoute.InvokeHandlersImpl(Object source, RoutedEventArgs args, Boolean reRaised)
   at System.Windows.UIElement.RaiseEventImpl(DependencyObject sender, RoutedEventArgs args)
   at System.Windows.BroadcastEventHelper.BroadcastEvent(DependencyObject root, RoutedEvent routedEvent)
   at System.Windows.BroadcastEventHelper.BroadcastLoadedEvent(Object root)
   at System.Windows.Media.MediaContext.FireLoadedPendingCallbacks()
   at System.Windows.Media.MediaContext.FireInvokeOnRenderCallbacks()
   at System.Windows.Media.MediaContext.RenderMessageHandlerCore(Object resizedCompositionTarget)
   at System.Windows.Media.MediaContext.RenderMessageHandler(Object resizedCompositionTarget)
   at System.Windows.Media.MediaContext.Resize(ICompositionTarget resizedCompositionTarget)
   at System.Windows.Interop.HwndTarget.OnResize()
   at System.Windows.Interop.HwndTarget.HandleMessage(WindowMessage msg, IntPtr wparam, IntPtr lparam)
   at System.Windows.Interop.HwndSource.HwndTargetFilterMessage(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
   at MS.Win32.HwndWrapper.WndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
   at MS.Win32.HwndSubclass.DispatcherCallbackOperation(Object o)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
   at System.Windows.Threading.Dispatcher.LegacyInvokeImpl(DispatcherPriority priority, TimeSpan timeout, Delegate method, Object args, Int32 numArgs)
   at System.Windows.Threading.Dispatcher.Invoke(DispatcherPriority priority, Delegate method, Object arg)
   at MS.Win32.HwndSubclass.SubclassWndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam)
   at MS.Win32.UnsafeNativeMethods.ShowWindow(HandleRef hWnd, Int32 nCmdShow)
   at MS.Win32.UnsafeNativeMethods.ShowWindow(HandleRef hWnd, Int32 nCmdShow)
   at System.Windows.Window.ShowHelper(Object booleanBox)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
   at System.Windows.Threading.DispatcherOperation.InvokeImpl()
   at MS.Internal.CulturePreservingExecutionContext.CallbackWrapper(Object obj)
   at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
   at MS.Internal.CulturePreservingExecutionContext.Run(CulturePreservingExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Windows.Threading.DispatcherOperation.Invoke()
   at System.Windows.Threading.Dispatcher.ProcessQueue()
   at System.Windows.Threading.Dispatcher.WndProcHook(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
   at MS.Win32.HwndWrapper.WndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
   at MS.Win32.HwndSubclass.DispatcherCallbackOperation(Object o)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
   at System.Windows.Threading.Dispatcher.LegacyInvokeImpl(DispatcherPriority priority, TimeSpan timeout, Delegate method, Object args, Int32 numArgs)
   at System.Windows.Threading.Dispatcher.Invoke(DispatcherPriority priority, Delegate method, Object arg)
   at MS.Win32.HwndSubclass.SubclassWndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at System.Windows.Threading.Dispatcher.PushFrameImpl(DispatcherFrame frame)
   at System.Windows.Application.RunDispatcher(Object ignore)
   at System.Windows.Application.RunInternal(Window window)
   at EliteInfoPanel.App.Main()
2025-05-17 16:17:53.381 +01:00 [INF] SummaryViewModel: Manual initialization requested
2025-05-17 16:17:53.382 +01:00 [INF] SummaryViewModel: InitializeAllItems called
2025-05-17 16:17:53.388 +01:00 [INF] UpdateShipItem called with values: ShipName=Type9, ShipLocalised=Type-9 Heavy, UserShipName=FastWad1, UserShipId=FW-1
2025-05-17 16:17:53.390 +01:00 [INF] CarrierJumpOverlay.UpdateVisibility: ShowCarrierJumpOverlay=false, JumpInProgress=false, CountdownSeconds=376, IsOnFleetCarrier=false, JumpArrived=false
2025-05-17 16:17:53.390 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-17 16:17:53.391 +01:00 [INF] Setting FleetCarrierJumpInProgress to false from Window_Loaded
2025-05-17 16:17:53.391 +01:00 [INF] SummaryViewModel: Manual initialization requested
2025-05-17 16:17:53.391 +01:00 [INF] SummaryViewModel: InitializeAllItems called
2025-05-17 16:17:53.392 +01:00 [INF] UpdateShipItem called with values: ShipName=Type9, ShipLocalised=Type-9 Heavy, UserShipName=FastWad1, UserShipId=FW-1
2025-05-17 16:17:53.470 +01:00 [INF] Applied full-screen settings on screen: \\.\DISPLAY2
2025-05-17 16:17:53.475 +01:00 [INF] MainViewModel: RefreshLayout called - forceRebuild=false
2025-05-17 16:17:53.475 +01:00 [INF] .... HyperspaceOverlay forced to hidden state
2025-05-17 16:17:53.476 +01:00 [INF] .... Disconnecting HyperspaceOverlay from previous GameState
2025-05-17 16:17:53.476 +01:00 [INF] .... Connecting HyperspaceOverlay to GameState
2025-05-17 16:17:53.476 +01:00 [INF] .... HyperspaceOverlay now HIDDEN
2025-05-17 16:17:53.476 +01:00 [INF] CarrierJumpOverlay.UpdateVisibility: ShowCarrierJumpOverlay=false, JumpInProgress=false, CountdownSeconds=0, IsOnFleetCarrier=false, JumpArrived=false
2025-05-17 16:17:53.476 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-17 16:17:53.813 +01:00 [INF] SummaryViewModel: InitializeAllItems called
2025-05-17 16:17:53.815 +01:00 [INF] UpdateShipItem called with values: ShipName=Type9, ShipLocalised=Type-9 Heavy, UserShipName=FastWad1, UserShipId=FW-1
2025-05-17 16:17:53.860 +01:00 [INF] FleetCarrierCard.SetContextVisibility(true), IsUserEnabled: true
2025-05-17 16:17:53.870 +01:00 [INF] Layout includes 6 cards: SummaryViewModel, CargoViewModel, RouteViewModel, FleetCarrierCargoViewModel, FlagsViewModel, ColonizationViewModel
2025-05-17 16:17:53.889 +01:00 [INF] MainViewModel: Layout refresh completed
2025-05-17 16:17:54.208 +01:00 [INF] FleetCarrierCard.SetContextVisibility(true), IsUserEnabled: true
2025-05-17 16:17:54.234 +01:00 [INF] Layout includes 6 cards: SummaryViewModel, CargoViewModel, RouteViewModel, FleetCarrierCargoViewModel, FlagsViewModel, ColonizationViewModel
2025-05-17 16:17:54.249 +01:00 [INF] MainViewModel: Layout refresh completed
2025-05-17 16:17:54.265 +01:00 [INF] CarrierJumpOverlay.UpdateVisibility: ShowCarrierJumpOverlay=false, JumpInProgress=false, CountdownSeconds=0, IsOnFleetCarrier=false, JumpArrived=false
2025-05-17 16:17:54.265 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-17 16:17:54.265 +01:00 [INF] Forced initial check of carrier jump overlay visibility
2025-05-17 16:17:54.265 +01:00 [INF] CarrierJumpOverlay.UpdateVisibility: ShowCarrierJumpOverlay=false, JumpInProgress=false, CountdownSeconds=0, IsOnFleetCarrier=false, JumpArrived=false
2025-05-17 16:17:54.265 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-17 16:17:54.265 +01:00 [INF] Forced initial check of carrier jump overlay visibility
