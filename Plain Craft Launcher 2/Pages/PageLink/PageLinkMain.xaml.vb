Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Net.Sockets
Imports System.Reflection
Imports PCL

Public Class PageLinkMain

    '===============================
    '  状态机与前端页面
    '===============================

#Region "状态管理"

    Public Enum LinkStates
        Waiting
        Loading
        Failed
        Finished
    End Enum
    Public Shared LinkState As LinkStates = LinkStates.Waiting

    ''' <summary>
    ''' 切换到指定的状态。
    ''' </summary>
    Private Sub ChangeState(NewState As LinkStates)
        If LinkState = NewState Then Return
        Dim OldState = LinkState
        LinkState = NewState
        Logger.Info($"主状态由 {OldState} 变更为 {NewState}")
        '触发状态切换
        RunInUi(
        Sub()
            Select Case NewState
                Case LinkStates.Waiting
                    SwitchToWaiting(OldState)
                Case LinkStates.Loading
                    SwitchToLoading(OldState)
                Case LinkStates.Failed
                    SwitchToFailed(OldState)
                Case LinkStates.Finished
                    SwitchToFinished(OldState)
            End Select
        End Sub)
    End Sub

    '让加载器的 成功/失败 事件触发状态切换
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, PanAlways, LinkLoader, AutoRun:=False)
        AddHandler LinkLoader.OnStateChangedUi,
        Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
            Select Case NewState
                Case LoadState.Finished
                    ChangeState(LinkStates.Finished)
                Case LoadState.Failed
                    Telemetry("联机失败", "Exception", FilterUserName(Loader.Error.GetDisplay(True), "*"))
                    ChangeState(LinkStates.Failed)
            End Select
        End Sub
    End Sub

    '页面动画兼容
    Private Sub UpdatePanelVisibility() Handles Me.PageEnter
        FrmLinkMain.PanSelect.Visibility = If(LinkState = LinkStates.Waiting, Visibility.Visible, Visibility.Collapsed)
        FrmLinkMain.PanFinish.Visibility = If(LinkState = LinkStates.Finished, Visibility.Visible, Visibility.Collapsed)
    End Sub

#End Region

#Region "初始页面 (Waiting)"

    '由任意状态切换到 Waiting
    Private Sub SwitchToWaiting(OldState As LinkStates)
        '加载器与进程状态
        LinkLoader.Cancel()
        SyncLock LinkLoader.LockState
            LinkLoader.State = LoadState.Waiting
        End SyncLock
        ProcessStop()
        '页面切换
        If OldState <> LinkStates.Finished Then
            '从完成页面退出时，PageOnContentExit 结束会触发 PageEnter 事件，这会调用 UpdatePanelVisibility
            '如果在现在提前调用了，会丢失完成页面的退出动画
            UpdatePanelVisibility()
        End If
        PageOnContentExit()
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '创建（陶瓦联机：直接在游戏内“对局域网开放”，无需输入端口）
    Private Sub Create_MouseLeftButtonUp() Handles PanSelectCreate.MouseLeftButtonUp
        IsServerSide = True
        Logger.Info("尝试创建陶瓦联机房间")
        '启动
        ChangeState(LinkStates.Loading)
    End Sub

    '加入
    Private Shared LastCode As String = Nothing
    ''' <summary>
    ''' 输入房间码，切换到联机页并立即加入房间。
    ''' </summary>
    Public Shared Sub Join() Handles PanSelectJoin.MouseLeftButtonUp
        Dim Code As String = MyMsgBoxInput("输入房间码", "输入房主发给你的房间码。",
            HintText:=If(String.IsNullOrEmpty(LastCode), "", "使用上一次的房间码"))
        If Not String.IsNullOrEmpty(LastCode) AndAlso Code IsNot Nothing AndAlso Code = "" Then Code = LastCode
        If Code Is Nothing Then Return
        Join(Code)
    End Sub
    Public Sub JoinInternal(Code As String)
        If Code Is Nothing Then Return
        '基础格式校验
        Dim ValidateResult = ValidateCodeFormat(Code)
        If ValidateResult IsNot Nothing Then
            Hint(ValidateResult, HintType.Red)
            Return
        End If
        Code = FixCodeFormat(Code)
        Logger.Info($"实际使用的房间码：{Code}")
        '基础信息
        IsServerSide = False
        RoomCode = Code
        '启动
        LastCode = Code
        ChangeState(LinkStates.Loading)
    End Sub
    Public Shared Function ValidateCodeFormat(Code As String) As String
        If Code Is Nothing Then Return "房间码为空！"
        Code = FixCodeFormat(Code)
        If Code.Length < 4 Then Return "房间码有误，请让房主使用陶瓦联机创建房间！"
        Return Nothing
    End Function
    Private Shared Function FixCodeFormat(Code As String) As String
        Code = Code.Between("【", "】").Between("[", "]") '从完整消息中提取
        Return Code.Trim()
    End Function

    '自动加入

    ''' <summary>
    ''' 切换到联机页并立即加入指定房间。
    ''' </summary>
    Public Shared Sub Join(Code As String)
        If LinkState <> LinkStates.Waiting Then
            Hint("你已经在联机房间中了！", HintType.Red)
        ElseIf FrmMain.PageCurrent = FormMain.PageType.Link Then
            FrmLinkMain.JoinInternal(Code)
        Else
            AutoJoinCode = Code
            FrmMain.PageChange(FormMain.PageType.Link, FormMain.PageSubType.LinkMain)
        End If
    End Sub
    '在进入页面时自动尝试加入房间
    Private Shared AutoJoinCode As String = Nothing
    Private Sub PanSelectJoin_Loaded() Handles PanSelectJoin.Loaded
        If AutoJoinCode Is Nothing Then Return
        If LinkState = LinkStates.Waiting Then JoinInternal(AutoJoinCode)
        AutoJoinCode = Nothing
    End Sub

#End Region

#Region "加载页面 (Loading | Failed)"

    '由 Waiting 或 Failed 状态切换到 Loading
    Private Sub SwitchToLoading(OldState As LinkStates)
        '清理变量
        PeopleCountValue = 0
        '加载器与进程状态
        LinkLoader.Start(IsForceRestart:=True)
        '页面切换会由 Loader 调用 MyPageRight 来触发
        'UI 更新
        UpdateProgressBar(0)
        LabLoadTitle.Text = If(IsServerSide, "创建房间中", "加入房间中")
        UpdateLoadingPage("正在初始化……", "准备初始化")
    End Sub

    '由 Loading 状态切换到 Failed
    Private Sub SwitchToFailed(OldState As LinkStates)
        '加载器与进程状态
        LinkLoader.Cancel()
        ProcessStop()
        'UI 更新
        UpdateProgressBar(1)
        '显示错误信息
        LabLoadTitle.Text = FailReason
        Dim Brief As String = LinkLoader.Error.GetDisplay(False)
        Dim InnerEx As Exception = LinkLoader.Error
        If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Do Until InnerEx.InnerException Is Nothing
            InnerEx = InnerEx.InnerException
            If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Loop
        LabLoadDesc.Text = If(Brief.StartsWithF("$"), Brief.TrimStart("$"), LinkLoader.Error.GetDisplay(True))
        Logger.Warn(LinkLoader.Error, LabLoadTitle.Text)
    End Sub

    '===============================
    '  页面 UI
    '===============================

    Private Sub UpdateLoadingPage(Title As String, FailBrief As String)
        If FailReason = FailBrief & "失败" Then Return
        FailReason = FailBrief & "失败"
        Logger.Info($"开始步骤：{Title}")
        RunInUiWait(
        Sub()
            If FrmLinkMain Is Nothing OrElse Not FrmLinkMain.LabLoadDesc.IsLoaded Then Return
            FrmLinkMain.LabLoadDesc.Text = Title
        End Sub)
    End Sub
    Private FailReason As String = "准备初始化"

    '取消加载
    Private Sub BtnLoadCancel_Click() Handles BtnLoadCancel.Click
        ChangeState(LinkStates.Waiting)
    End Sub

    '点击重试
    Private Sub CardLoad_MouseLeftButtonUp() Handles CardLoad.MouseLeftButtonUp
        ChangeState(LinkStates.Loading)
    End Sub

    '进度条
    Private Sub UpdateProgressBar(Optional Value As Double = -1)
        If Value = -1 Then Value = LinkLoader.Progress
        Dim DisplayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(Value - DisplayingProgress, 3) = 0 Then Return
        If DisplayingProgress <= Value Then
            Dim NewProgress As Double = If(Value = 1, 1, DisplayingProgress +
                (Value - DisplayingProgress) ^ 2 * 0.5)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 200),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 200)
            }, "Link Progress")
        Else
            ColumnProgressA.Width = New GridLength(Value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - Value, GridUnitType.Star)
            AniStop("Link Progress")
        End If
    End Sub
    Private Sub CardLoad_SizeChanged() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "完成页面 (Finished)"

    '由 Loading 状态切换到 Finished
    Private Sub SwitchToFinished(OldState As LinkStates)
        '由于只可能从加载器的完成事件触发，不需要管加载器
        UpdatePanelVisibility() '页面的实际切换会由 Loader 调用 MyPageRight 来触发

        'UI 更新
        If IsServerSide Then
            LabFinishTitle.Text = "已创建房间"
            LabFinishDesc.Text = $"把房间码发给朋友，让大家加入房间吧！{vbCrLf}房间码：{GetInviteCode()}"
            BtnFinishExit.Text = "关闭"
            BtnFinishPing.ToolTip = "联机状态"
            BtnFinishCopy.Visibility = Visibility.Visible
            Copy() '立即复制房间码
            '下边栏
            BtnFinishPort.Visibility = Visibility.Collapsed
            LabFinishPort.Visibility = Visibility.Collapsed
            BtnFinishIp.Visibility = Visibility.Collapsed
            LabFinishIp.Visibility = Visibility.Collapsed
        Else
            LabFinishTitle.Text = "已加入房间"
            LabFinishDesc.Text = $"在多人游戏服务器地址填入下面的地址，即可进入房主的游戏！{vbCrLf}注意：使用离线登录时不要手动输入其他 IP！"
            BtnFinishExit.Text = "离开"
            BtnFinishPing.ToolTip = "联机状态"
            BtnFinishCopy.Visibility = Visibility.Collapsed
            '下边栏
            BtnFinishPort.Visibility = Visibility.Collapsed
            LabFinishPort.Visibility = Visibility.Collapsed
            LabFinishIp.Text = ClientAddress
            BtnFinishIp.Visibility = Visibility.Visible
            LabFinishIp.Visibility = Visibility.Visible
        End If
        Update() '立即刷新
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '退出
    Private Sub BtnFinishExit_Click() Handles BtnFinishExit.Click
        TryExit(False, False)
    End Sub
    ''' <summary>
    ''' 退出联机。
    ''' 返回是否弹出了警告窗口并且玩家选择了取消。
    ''' </summary>
    Public Function TryExit(Silent As Boolean, Closing As Boolean) As Boolean
        If LinkState = LinkStates.Waiting OrElse LinkState = LinkStates.Failed Then Return False
        If Not Silent Then
            If IsServerSide AndAlso PeopleCountValue > 1 Then
                If MyMsgBox("你确定要关闭联机房间吗？" & vbCrLf & "所有玩家都需要重新输入房间码才可加入游戏！", "退出联机", "确定", "取消", IsWarn:=True) = 2 Then Return True
            ElseIf Closing Then
                If MyMsgBox(If(IsServerSide, "你确定要关闭联机房间吗？", "你确定要离开联机房间吗？"), "退出联机", "确定", "取消") = 2 Then Return True
            End If
        End If
        ChangeState(LinkStates.Waiting)
        Return False
    End Function

    '复制房间码
    Private Sub Copy() Handles BtnFinishCopy.Click
        Dim CodeText As String = $"在 PCL 启动器中输入房间码【{GetInviteCode()}】，即可加入联机房间！"
        ClipboardSet(CodeText, False)
        Settings.Set("LinkLastAutoJoinInviteCode", CodeText)
        Hint("已复制房间码！", HintType.Green)
    End Sub
    Private Function GetInviteCode() As String
        Return RoomCode
    End Function

    '复制 IP
    Private Sub BtnFinishIp_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnFinishIp.MouseLeftButtonUp
        ClipboardSet(ClientAddress, False)
        Hint("已复制服务器地址！", HintType.Green)
    End Sub

#End Region

    '===============================
    '  后端（陶瓦联机 Terracotta）
    '===============================

    ''' <summary>
    ''' 陶瓦联机（Terracotta）当前版本，与 HMCL 内置版本对齐。
    ''' </summary>
    Private Const TerracottaVersion As String = "0.4.2"
    ''' <summary>
    ''' Terracotta 所在的文件夹路径，以 \ 结尾。
    ''' </summary>
    Private PathTerracotta As String = Paths.AppDataThenName & "Terracotta\"
    ''' <summary>
    ''' 当前是服务端还是客户端。
    ''' </summary>
    Private IsServerSide As Boolean
    ''' <summary>
    ''' 当前使用的 Terracotta 分类（windows-x86_64 / windows-arm64）。
    ''' </summary>
    Private TerracottaClassifier As String = "windows-x86_64"
    ''' <summary>
    ''' Terracotta 可执行文件名。
    ''' </summary>
    Private TerracottaExeName As String = ""
    ''' <summary>
    ''' 房间码（房主创建成功后由陶瓦联机服务生成）。
    ''' </summary>
    Private RoomCode As String = ""
    ''' <summary>
    ''' 客户端映射后的服务器地址（加入成功后由陶瓦联机服务给出）。
    ''' </summary>
    Private ClientAddress As String = Nothing
    ''' <summary>
    ''' 陶瓦联机本地 HTTP 服务端口。
    ''' </summary>
    Private Shared TerracottaPort As Integer = 0
    ''' <summary>
    ''' 陶瓦联机公共节点列表。
    ''' </summary>
    Private Shared PublicNodes As New List(Of String)

#Region "加载"

    Private WithEvents LinkLoader As New LoaderCombo(Of Integer)("联机", {
        New LoaderTask(Of Integer, Integer)("获取陶瓦联机节点", AddressOf InitConfig) With {.ProgressWeight = 8},
        New LoaderTask(Of Integer, List(Of NetFile))("准备下载陶瓦联机模块", AddressOf InitPrepareDownload) With {.ProgressWeight = 2},
        New LoaderDownload("下载陶瓦联机模块", New List(Of NetFile)) With {.ProgressWeight = 40},
        New LoaderTask(Of Integer, Integer)("启动陶瓦联机模块", AddressOf InitLaunch) With {.ProgressWeight = 50}
    })

    '1. 获取陶瓦联机公共节点列表
    Private Sub InitConfig(Task As LoaderTask(Of Integer, Integer))
        UpdateLoadingPage("正在获取陶瓦联机节点……", "获取陶瓦联机节点")
        Try
            Dim Raw As JArray = NetRequestByClient("https://terracotta.glavo.site/nodes", RequireJson:=True).DeserializeJson()
            PublicNodes = Raw.Select(Function(Node) Node("url").ToString).Distinct().ToList()
            Logger.Info($"获取到陶瓦联机节点 {PublicNodes.Count} 个")
        Catch ex As Exception
            Logger.Warn(ex, "获取陶瓦联机节点列表失败，将尝试直接连接")
            PublicNodes = New List(Of String)
        End Try
        Task.Output = 0
    End Sub

    '2. 获取需要下载的文件
    Private Sub InitPrepareDownload(Task As LoaderTask(Of Integer, List(Of NetFile)))
        '获取 CPU 架构
        UpdateLoadingPage("正在获取 CPU 架构……", "获取 CPU 架构")
        Dim Architecture As String = GetType(String).Assembly.GetName().ProcessorArchitecture
        Select Case Architecture
            Case ProcessorArchitecture.Amd64
                Architecture = "x86_64"
            Case ProcessorArchitecture.Arm
                Architecture = "arm64"
            Case Else
                Throw New Exception($"$陶瓦联机暂不支持 {Architecture} 架构，请使用 64 位 Windows 系统。")
        End Select
        TerracottaClassifier = $"windows-{Architecture}"
        Logger.Info($"CPU 架构：{Architecture}")
        Telemetry("联机开始")
        '检查 Terracotta 版本
        UpdateLoadingPage("正在检查陶瓦联机版本……", "检查陶瓦联机版本")
        TerracottaExeName = $"terracotta-{TerracottaVersion}-{TerracottaClassifier}.exe"
        If Not (FileUtils.Exists(PathTerracotta & TerracottaExeName) AndAlso FileUtils.Exists(PathTerracotta & "VCRUNTIME140.DLL")) Then
            Settings.Set("LinkTerracottaVersion", -1)
        End If
        Dim LocalVersion As Integer = Settings.Get(Of Integer)("LinkTerracottaVersion")
        Dim RequiredFiles As New List(Of NetFile)
        Logger.Info($"陶瓦联机本地版本：{LocalVersion}，需求版本：{TerracottaVersion}")
        If LocalVersion < 0 Then
            RequiredFiles.Add(New NetFile({
                $"https://github.com/burningtnt/Terracotta/releases/download/v{TerracottaVersion}/terracotta-{TerracottaVersion}-{TerracottaClassifier}-pkg.tar.gz",
                $"https://gitee.com/burningtnt/Terracotta/releases/download/v{TerracottaVersion}/terracotta-{TerracottaVersion}-{TerracottaClassifier}-pkg.tar.gz",
                $"https://cnb.cool/HMCL-Terracotta/Terracotta/-/releases/download/v{TerracottaVersion}/terracotta-{TerracottaVersion}-{TerracottaClassifier}-pkg.tar.gz",
                $"https://alist.8mi.tech/d/mirror/HMCL-Terracotta/Auto/v{TerracottaVersion}/terracotta-{TerracottaVersion}-{TerracottaClassifier}-pkg.tar.gz"
            }, PathTerracotta & "Terracotta.tar.gz",
               New FileChecker With {.MinSize = 1024 * 1024}))
        End If
        '开始下载
        UpdateLoadingPage("正在下载陶瓦联机模块……", "下载陶瓦联机模块")
        Task.Output = RequiredFiles
    End Sub

    '3. 启动陶瓦联机模块
    Private Sub InitLaunch(Task As LoaderTask(Of Integer, Integer))
        '解压文件
        UpdateLoadingPage("正在解压陶瓦联机模块……", "解压陶瓦联机模块")
        If FileUtils.Exists(PathTerracotta & "Terracotta.tar.gz") Then
            ExtractTarGz(PathTerracotta & "Terracotta.tar.gz", PathTerracotta)
            '若解压到了子目录，则将文件平铺到根目录
            If Not FileUtils.Exists(PathTerracotta & TerracottaExeName) Then
                Dim FoundExe = Directory.GetFiles(PathTerracotta, TerracottaExeName, SearchOption.AllDirectories).FirstOrDefault()
                If FoundExe IsNot Nothing Then FileUtils.Move(FoundExe, PathTerracotta & TerracottaExeName)
            End If
            If Not FileUtils.Exists(PathTerracotta & "VCRUNTIME140.DLL") Then
                Dim FoundDll = Directory.GetFiles(PathTerracotta, "VCRUNTIME140.DLL", SearchOption.AllDirectories).FirstOrDefault()
                If FoundDll IsNot Nothing Then FileUtils.Move(FoundDll, PathTerracotta & "VCRUNTIME140.DLL")
            End If
            FileUtils.Delete(PathTerracotta & "Terracotta.tar.gz")
            Settings.Set("LinkTerracottaVersion", 1)
        End If
        Task.Progress = 0.08
        '启动进程
        UpdateLoadingPage("正在启动陶瓦联机模块……", "启动陶瓦联机模块")
        Dim HttpFileDir As String = Path.Combine(RequestTaskTempFolder(), "TerracottaHttp")
        Directory.CreateDirectory(HttpFileDir)
        Dim HttpFile As String = Path.Combine(HttpFileDir, "http")
        ProcessStart(PathTerracotta & TerracottaExeName, $"--hmcl ""{HttpFile}""")
        Task.Progress = 0.15
        '等待 HTTP 服务端口文件出现
        UpdateLoadingPage("正在连接陶瓦联机服务……", "连接陶瓦联机服务")
        Dim FileTimeout As Long = Environment.TickCount + 20000
        Dim ProcessExitedTime As Long = Long.MaxValue
        Do While Environment.TickCount < FileTimeout
            If File.Exists(HttpFile) Then Exit Do
            If ProcessCore IsNot Nothing AndAlso ProcessCore.HasExited Then
                '陶瓦联机支持单实例委托：新实例写端口文件后立即退出，由常驻实例提供服务，
                '因此进程退出本身不算崩溃；只有端口文件迟迟未出现时才按启动失败处理。
                If ProcessExitedTime = Long.MaxValue Then ProcessExitedTime = Environment.TickCount
                If Environment.TickCount - ProcessExitedTime >= 10000 Then
                    Panic("无法启动陶瓦联机模块", "近期日志：" & vbCrLf & LogHistory.Join(vbCrLf))
                    Throw New OperationCanceledException
                End If
            End If
            Thread.Sleep(300)
        Loop
        If Not File.Exists(HttpFile) Then Throw New Exception("$无法启动陶瓦联机模块。")
        TerracottaPort = CType(File.ReadAllText(HttpFile).DeserializeJson()("port"), Integer)
        Logger.Info($"陶瓦联机本地 HTTP 端口：{TerracottaPort}")
        Task.Progress = 0.25
        '发起创建/加入请求
        If IsServerSide Then
            UpdateLoadingPage("正在创建房间……", "创建房间")
        Else
            UpdateLoadingPage("正在加入房间……", "加入房间")
        End If
        Dim Query As String = "player=" & WebUtility.UrlEncode(GetPlayerName())
        For Each Node In PublicNodes
            Query &= "&public_nodes=" & WebUtility.UrlEncode(Node)
        Next
        If IsServerSide Then
            NetRequestByClient($"http://127.0.0.1:{TerracottaPort}/state/scanning?{Query}")
        Else
            NetRequestByClient($"http://127.0.0.1:{TerracottaPort}/state/guesting?room={WebUtility.UrlEncode(RoomCode)}&{Query}")
        End If
        '轮询状态直到成功或超时
        Dim StateTimeout As Long = Environment.TickCount + 120000
        Do
            Thread.Sleep(500)
            CheckCrash()
            Dim Json As JObject
            Try
                Json = NetRequestByClient($"http://127.0.0.1:{TerracottaPort}/state", RequireJson:=True).DeserializeJson()
            Catch ex As Exception
                ex.ThrowIfCanceled()
                Logger.Warn(ex, "获取陶瓦联机状态失败")
                Continue Do
            End Try
            Select Case Json("state").ToString
                Case "host-ok"
                    RoomCode = Json("code").ToString
                    Task.Progress = 1
                    Logger.Info($"已创建房间，房间码：{RoomCode}")
                    Return
                Case "guest-ok"
                    ClientAddress = Json("url").ToString
                    Task.Progress = 1
                    Logger.Info($"已加入房间，服务器地址：{ClientAddress}")
                    Return
                Case "exception"
                    Dim TypeIndex As Integer = Json("type").ToObject(Of Integer)
                    Panic("陶瓦联机出错", TerracottaExceptionTypes(Math.Min(Math.Max(TypeIndex, 0), TerracottaExceptionTypes.Length - 1)))
                    Throw New OperationCanceledException
                Case Else
                    Task.Progress = (Task.Progress + 0.02).Clamp(0.25, 0.95)
            End Select
        Loop Until Task.IsCanceled OrElse Environment.TickCount > StateTimeout
        If Task.IsCanceled Then Throw New OperationCanceledException
        Panic("连接超时", "无法完成陶瓦联机连接，请检查你的网络环境后重试。")
        Throw New OperationCanceledException
    End Sub

    Private Shared ReadOnly TerracottaExceptionTypes As String() = {
        "无法连接到房主",
        "与房主的连接被拒绝",
        "联机模块崩溃",
        "联机模块崩溃",
        "服务器连接被重置",
        "陶瓦联机服务响应无效"
    }

    ''' <summary>
    ''' 解压 tar.gz 文件到目标目录（自动忽略目录条目与路径穿越）。
    ''' </summary>
    Private Sub ExtractTarGz(TarGzPath As String, TargetDir As String)
        Dim FullTarget As String = Path.GetFullPath(TargetDir)
        Using Gz As New GZipStream(File.OpenRead(TarGzPath), CompressionMode.Decompress)
            Dim Header(511) As Byte
            Do
                Dim HeaderRead As Integer = Gz.Read(Header, 0, 512)
                If HeaderRead < 512 Then Exit Do
                If Header.All(Function(B) B = 0) Then Exit Do '结尾的零块
                '文件名（0-99）与大小（124-135，八进制）
                Dim Name As String = Encoding.ASCII.GetString(Header, 0, 100).TrimEnd(Chr(0), " "c).TrimStart("./")
                Dim SizeOctal As String = Encoding.ASCII.GetString(Header, 124, 12).TrimEnd(Chr(0), " "c, vbNullChar)
                If SizeOctal = "" Then Continue Do
                Dim Size As Long = Convert.ToInt64(SizeOctal, 8)
                If Name = "" OrElse Name = "." Then
                    '目录条目：跳过数据
                    SkipTarData(Gz, Size)
                    Continue Do
                End If
                Dim FullPath As String = Path.GetFullPath(Path.Combine(TargetDir, Name))
                If FullPath.StartsWith(FullTarget, StringComparison.OrdinalIgnoreCase) Then
                    Directory.CreateDirectory(Path.GetDirectoryName(FullPath))
                    Using Out As FileStream = File.Create(FullPath)
                        Dim Buffer(65535) As Byte
                        Dim Remaining As Long = Size
                        Do While Remaining > 0
                            Dim ReadCount As Integer = Gz.Read(Buffer, 0, CInt(Math.Min(Remaining, Buffer.Length)))
                            If ReadCount = 0 Then Exit Do
                            Out.Write(Buffer, 0, ReadCount)
                            Remaining -= ReadCount
                        Loop
                    End Using
                End If
                '跳过对齐填充
                SkipTarData(Gz, (512 - (Size Mod 512)) Mod 512)
            Loop
        End Using
    End Sub
    Private Sub SkipTarData(Gz As GZipStream, Length As Long)
        Dim Buffer(1023) As Byte
        Dim Remaining As Long = Length
        Do While Remaining > 0
            Dim ReadCount As Integer = Gz.Read(Buffer, 0, CInt(Math.Min(Remaining, Buffer.Length)))
            If ReadCount = 0 Then Exit Do
            Remaining -= ReadCount
        Loop
    End Sub

    Private Function GetPlayerName() As String
        Dim Name As String = Environment.UserName
        If String.IsNullOrWhiteSpace(Name) Then Name = "Player"
        Return Name
    End Function

#End Region

#Region "进程与日志"

    ''' <summary>
    ''' Terracotta 进程。
    ''' </summary>
    Private Shared ProcessCore As Process
    Private Shared ProcessOutputHandle As AutoResetEvent, ProcessErrorHandle As AutoResetEvent

    ''' <summary>
    ''' 若程序正在运行，则结束程序进程。
    ''' 即使失败也不会抛出异常。
    ''' </summary>
    Public Shared Sub ProcessStop()
        Try
            '关闭未捕获的进程
            For Each ProcessObject In Process.GetProcesses
                If ProcessObject.Id = ProcessCore?.Id Then Continue For
                If Not ProcessObject.ProcessName.StartsWith("terracotta", StringComparison.OrdinalIgnoreCase) Then Continue For
                Try
                    Logger.Info($"停止残留的陶瓦联机模块，PID：{ProcessObject.Id}，进程名：{ProcessObject.ProcessName}")
                    ProcessObject.Kill()
                    ProcessObject.Close()
                Catch exx As Exception
                    Logger.Warn(exx, $"结束进程失败（{ProcessObject.ProcessName}，PID {ProcessObject.Id}）")
                End Try
            Next
            '关闭由自身启动的进程
            If ProcessCore IsNot Nothing AndAlso Not ProcessCore.HasExited Then
                Logger.Info($"停止所启动的陶瓦联机模块，PID：{ProcessCore.Id}")
                ProcessCore.Kill()
                ProcessCore.Close()
                ProcessCore = Nothing
            End If
            '释放资源
            ProcessOutputHandle?.Dispose()
            ProcessErrorHandle?.Dispose()
        Catch ex As Exception
            Logger.Warn(ex, "停止陶瓦联机模块失败")
        End Try
    End Sub
    ''' <summary>
    ''' 出现意外错误，给出错误信息并结束联机。
    ''' </summary>
    Private Sub Panic(Brief As String, Detail As String)
        '显示信息
        If LinkState = LinkStates.Loading Then
            FailReason = Brief
            LinkLoader.Failed(New Exception(Detail))
        Else
            MyMsgBox(Detail, "联机出错：" & Brief, IsWarn:=True)
            ChangeState(LinkStates.Waiting)
            Telemetry("联机失败", "Exception", FilterUserName(Brief & "：" & Detail, "*"))
        End If
    End Sub

    ''' <summary>
    ''' 启动进程。
    ''' </summary>
    Private Sub ProcessStart(ExePath As String, Arguments As String)
        Dim Info = New ProcessStartInfo With {
            .FileName = ExePath,
            .Arguments = Arguments,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardInput = True,
            .RedirectStandardError = True,
            .RedirectStandardOutput = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        Logger.Info($"正在启动陶瓦联机：{Arguments}")
        ProcessCore = New Process With {.StartInfo = Info}
        Dim LogLineHandler =
        Sub(sender As Object, e As DataReceivedEventArgs, Handle As AutoResetEvent)
            Try
                If e.Data Is Nothing Then
                    Handle.[Set]()
                    Return
                End If
                ProcessLogLine(e.Data)
            Catch unused As ObjectDisposedException
            Catch ex As Exception
                Logger.Warn(ex, "读取陶瓦联机信息失败")
            End Try
        End Sub
        ProcessOutputHandle = New AutoResetEvent(False)
        AddHandler ProcessCore.OutputDataReceived, Sub(sender, e) LogLineHandler(sender, e, ProcessOutputHandle)
        ProcessErrorHandle = New AutoResetEvent(False)
        AddHandler ProcessCore.ErrorDataReceived, Sub(sender, e) LogLineHandler(sender, e, ProcessErrorHandle)
        ProcessCore.Start()
        ProcessCore.BeginOutputReadLine()
        ProcessCore.BeginErrorReadLine()
    End Sub
    Private Sub ProcessLogLine(Line As String)
        '记录日志
        Logger.Info(Line)
        LogHistory.Enqueue(Line)
        If LogHistory.Count >= 10 Then LogHistory.Dequeue()
    End Sub
    Private LogHistory As New Queue(Of String)(10)

#End Region

#Region "状态刷新"

    ''' <summary>
    ''' 当前房间中的人数，包括自己。
    ''' </summary>
    Private PeopleCountValue As Integer = 0

#End Region

#Region "定时任务"

    '启动
    Private Sub StartTimerThread() Handles Me.Loaded
        Static IsTimerStarted As Boolean = False
        If IsTimerStarted Then Return
        RunInNewThread(
        Sub()
            Dim Counter As Integer = 0
            Do While True
                Try
                    Thread.Sleep(200)
                    Counter += 200
                    '每秒执行一次
                    If Counter >= 1000 Then
                        Counter = 0
                        Update()
                    End If
                    '每 200ms 更新进度条
                    If LinkState = LinkStates.Loading Then RunInUi(AddressOf UpdateProgressBar)
                Catch ex As Exception
                    Logger.Error(ex, "联机模块主时钟出错")
                    Thread.Sleep(10000)
                End Try
            Loop
        End Sub, "Link Timer")
    End Sub

    '每秒或进入页面时触发
    Private Sub Update() Handles Me.Loaded
        If LinkState <> LinkStates.Finished Then Return
        '从陶瓦联机服务获取最新状态
        If TerracottaPort > 0 Then
            Try
                Dim Json As JObject = NetRequestByClient($"http://127.0.0.1:{TerracottaPort}/state", RequireJson:=True).DeserializeJson()
                Dim StateName As String = Json("state").ToString
                Dim Profiles As JArray = Json("profiles")
                If Profiles IsNot Nothing Then PeopleCountValue = Profiles.Count
                '房主离开检测（客户端）
                If Not IsServerSide AndAlso (StateName = "waiting" OrElse StateName = "exception") Then
                    MyMsgBox("房主已离开房间！", "联机结束")
                    ChangeState(LinkStates.Waiting)
                    Return
                End If
            Catch ex As Exception
                Logger.Warn(ex, "获取陶瓦联机状态失败")
            End Try
        End If
        '检查核心状态
        CheckCrash()
        '更新 UI
        If FrmMain.PageCurrent = FormMain.PageType.Link Then
            RunInUi(
            Sub()
                'Logo 旋转动画
                AniStart(AaRotateTransform(ImgFinishLogo, 500, 5000), "Link Logo Rotation")
                '状态显示
                LabFinishPing.Text = "联机正常"
                '人数显示
                LabFinishPlayer.Text = PeopleCountValue & " 人"
                '提示条
                HintFinish.Visibility = Visibility.Collapsed
            End Sub)
        End If
    End Sub
    ''' <summary>
    ''' 检查联机模块是否崩溃。
    ''' </summary>
    Private Sub CheckCrash()
        '端口文件已出现即视为联机模块可用（陶瓦联机支持单实例委托：本实例的进程可能已退出，由常驻实例继续服务）
        If TerracottaPort > 0 Then Return
        If ProcessCore IsNot Nothing AndAlso Not ProcessCore.HasExited Then Return
        Panic("陶瓦联机模块已崩溃", "近期日志：" & vbCrLf & LogHistory.Join(vbCrLf))
    End Sub

#End Region

End Class
