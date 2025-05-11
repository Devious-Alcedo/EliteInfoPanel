 when chaging screen mode background turns black - FIXED
 ship cargo items not themed correctly
 if no carrier cargo exists for a commodity, and that commodity is added to the ships cargo it does not dispaly ship cargo amount in colonization card
 need to improve theming


 2025-05-11 15:44:09.106 +01:00 [INF] Carrier jump countdown reached zero - preparing for jump
2025-05-11 15:44:09.107 +01:00 [INF] .... FleetCarrierJumpInProgress changed to true - Stack trace:    at System.Environment.get_StackTrace()
   at EliteInfoPanel.Core.GameStateService.set_FleetCarrierJumpInProgress(Boolean value) in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 394
   at EliteInfoPanel.Core.GameStateService.get_CarrierJumpCountdownSeconds() in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 270
   at EliteInfoPanel.Core.GameStateService.get_ShowCarrierJumpOverlay() in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 532
   at EliteInfoPanel.Controls.CarrierJumpOverlay.<SetGameState>b__20_0(Object s, EventArgs e) in C:\Users\jimmy\source\repos\EliteInfoPanel\Controls\CarrierJumpOverlay.xaml.cs:line 99
   at System.Windows.Threading.DispatcherTimer.FireTick()
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
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
   at System.Windows.Threading.Dispatcher.LegacyInvokeImpl(DispatcherPriority priority, TimeSpan timeout, Delegate method, Object args, Int32 numArgs)
   at MS.Win32.HwndSubclass.SubclassWndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at System.Windows.Threading.Dispatcher.PushFrameImpl(DispatcherFrame frame)
   at System.Windows.Application.RunDispatcher(Object ignore)
   at System.Windows.Application.RunInternal(Window window)
   at EliteInfoPanel.App.Main()
2025-05-11 15:44:09.107 +01:00 [INF] ShowCarrierJumpOverlay calculation (detailed): FleetCarrierJumpInProgress=true, IsOnFleetCarrier=true, CountdownSeconds=0, JumpArrived=false, CarrierJumpDestSystem=Bletii, CurrentStationName=JNZ-4XN, Result=true
2025-05-11 15:44:09.107 +01:00 [INF] Jump animation starting - forced overlay display
2025-05-11 15:44:09.107 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=true, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:09.107 +01:00 [INF] Jump animation starting - forced overlay display
2025-05-11 15:44:09.107 +01:00 [INF] Overlay becoming VISIBLE (conditions met)
2025-05-11 15:44:09.108 +01:00 [INF] Starting CarrierJumpOverlay render thread
2025-05-11 15:44:09.111 +01:00 [INF] ShowCarrierJumpOverlay calculation (detailed): FleetCarrierJumpInProgress=true, IsOnFleetCarrier=true, CountdownSeconds=0, JumpArrived=false, CarrierJumpDestSystem=Bletii, CurrentStationName=JNZ-4XN, Result=true
2025-05-11 15:44:09.111 +01:00 [INF] ShowCarrierJumpOverlay calculation (detailed): FleetCarrierJumpInProgress=true, IsOnFleetCarrier=true, CountdownSeconds=0, JumpArrived=false, CarrierJumpDestSystem=Bletii, CurrentStationName=JNZ-4XN, Result=true
2025-05-11 15:44:09.111 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=true, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:09.111 +01:00 [INF] ShowCarrierJumpOverlay calculation (detailed): FleetCarrierJumpInProgress=true, IsOnFleetCarrier=true, CountdownSeconds=0, JumpArrived=false, CarrierJumpDestSystem=Bletii, CurrentStationName=JNZ-4XN, Result=true
2025-05-11 15:44:09.111 +01:00 [INF] Overlay becoming VISIBLE (conditions met)
2025-05-11 15:44:09.250 +01:00 [INF] .... FleetCarrierJumpInProgress changed to false - Stack trace:    at System.Environment.get_StackTrace()
   at EliteInfoPanel.Core.GameStateService.set_FleetCarrierJumpInProgress(Boolean value) in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 394
   at EliteInfoPanel.Core.GameStateService.ProcessJournalAsync() in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 1930
   at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[TStateMachine](TStateMachine& stateMachine)
   at EliteInfoPanel.Core.GameStateService.ProcessJournalAsync()
   at EliteInfoPanel.Core.GameStateService.<>c__DisplayClass262_0.<<SetupJournalWatcher>b__0>d.MoveNext() in C:\Users\jimmy\source\repos\EliteInfoPanel\Core\GameStateService.cs:line 2444
   at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[TStateMachine](TStateMachine& stateMachine)
   at EliteInfoPanel.Core.GameStateService.<>c__DisplayClass262_0.<SetupJournalWatcher>b__0(Object s, EventArgs e)
   at System.Windows.Threading.DispatcherTimer.FireTick()
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
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
   at System.Windows.Threading.Dispatcher.LegacyInvokeImpl(DispatcherPriority priority, TimeSpan timeout, Delegate method, Object args, Int32 numArgs)
   at MS.Win32.HwndSubclass.SubclassWndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at MS.Win32.UnsafeNativeMethods.DispatchMessage(MSG& msg)
   at System.Windows.Threading.Dispatcher.PushFrameImpl(DispatcherFrame frame)
   at System.Windows.Application.RunDispatcher(Object ignore)
   at System.Windows.Application.RunInternal(Window window)
   at EliteInfoPanel.App.Main()
2025-05-11 15:44:09.250 +01:00 [INF] SummaryViewModel: Manual initialization requested
2025-05-11 15:44:09.251 +01:00 [INF] SummaryViewModel: InitializeAllItems called
2025-05-11 15:44:09.252 +01:00 [INF] UpdateShipItem called with values: ShipName=Type9, ShipLocalised=Type-9 Heavy, UserShipName=FastWad1, UserShipId=FW-1
2025-05-11 15:44:09.253 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=false, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:09.253 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-11 15:44:09.253 +01:00 [INF] Stopping CarrierJumpOverlay render thread
2025-05-11 15:44:09.268 +01:00 [INF] MainWindow detected ShowCarrierJumpOverlay changed to false
2025-05-11 15:44:09.268 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=false, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:09.268 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-11 15:44:09.268 +01:00 [INF] MainWindow detected ShowCarrierJumpOverlay changed to false
2025-05-11 15:44:10.072 +01:00 [INF] Carrier jump countdown reached zero ... notifying GameStateService
2025-05-11 15:44:10.072 +01:00 [INF] ... Conditions met for carrier jump overlay
2025-05-11 15:44:10.073 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=false, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:10.073 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-11 15:44:10.073 +01:00 [INF] MainWindow detected ShowCarrierJumpOverlay changed to false
2025-05-11 15:44:10.073 +01:00 [INF] UpdateVisibility: ShowCarrierJumpOverlay=false, CountdownSeconds=0, IsOnFleetCarrier=true
2025-05-11 15:44:10.073 +01:00 [INF] Overlay HIDDEN ... conditions not met
2025-05-11 15:44:10.073 +01:00 [INF] MainWindow detected ShowCarrierJumpOverlay changed to false
