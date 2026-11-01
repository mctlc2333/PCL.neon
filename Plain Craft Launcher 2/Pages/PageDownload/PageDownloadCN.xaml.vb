Public Class PageDownloadCN

    Private IsDownloading As Boolean = False
    Private IsCanceled As Boolean = False
    Private HasLoaded As Boolean = False

    Private Sub PageInit() Handles Me.Initialized
        DisabledPageAnimControls.Add(BtnStart)
        DisabledPageAnimControls.Add(BtnStop)
        DisabledPageAnimControls.Add(ComboVersion)
        DisabledPageAnimControls.Add(RadioNet)
        DisabledPageAnimControls.Add(RadioRental)
    End Sub

    Private Sub PageLoad() Handles Me.Loaded
        If HasLoaded Then Return
        HasLoaded = True
        RefreshLoginState()
        ' 已登录时立即自动查询并开始下载（未登录也允许下载客户端本体）
        StartDownload()
    End Sub

    ''' <summary>
    ''' 供下载页左侧"刷新"按钮调用：刷新登录状态；若当前未在下载，则按当前选择重新开始。
    ''' </summary>
    Public Sub RefreshDownload()
        RefreshLoginState()
        If Not IsDownloading Then StartDownload()
    End Sub

    Private Sub RefreshLoginState()
        Dim UserId As String = Settings.Get(Of String)("CacheX19Uuid")
        Dim UserToken As String = Settings.Get(Of String)("CacheX19Access")
        If UserId <> "" AndAlso UserToken <> "" Then
            LabLogin.Text = "已检测到网易/4399 登录态，可同时下载白端基础模组。"
        Else
            LabLogin.Text = "未检测到网易/4399 登录态：仅下载客户端本体；如需白端基础模组，请先在启动页登录网易中国版或 4399 账号。"
        End If
    End Sub

    ' ============================ 选择即下载 ============================

    Private Sub ComboVersion_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If Not HasLoaded Then Return
        If IsDownloading Then Return
        StartDownload()
    End Sub

    Private Sub RadioNet_Checked(sender As Object, e As RoutedEventArgs)
        If Not HasLoaded Then Return
        If IsDownloading Then Return
        StartDownload()
    End Sub

    Private Sub RadioRental_Checked(sender As Object, e As RoutedEventArgs)
        If Not HasLoaded Then Return
        If IsDownloading Then Return
        StartDownload()
    End Sub

    Private Sub BtnStart_Click(sender As Object, e As MouseButtonEventArgs)
        StartDownload()
    End Sub

    Private Sub BtnStop_Click(sender As Object, e As MouseButtonEventArgs)
        IsCanceled = True
        AppendLog("正在停止…（将在当前文件下载完成后中止）")
    End Sub

    ' ============================ 下载执行 ============================

    Private Sub StartDownload()
        If IsDownloading Then Return
        Dim McVersion As Long = 1021000
        Dim VersionName As String = "1.21"
        If ComboVersion.SelectedItem IsNot Nothing Then
            Dim Item As MyComboBoxItem = CType(ComboVersion.SelectedItem, MyComboBoxItem)
            If Item.Tag IsNot Nothing AndAlso Val(Item.Tag.ToString()) > 0 Then McVersion = Val(Item.Tag.ToString())
            VersionName = Item.Content.ToString()
        End If
        Dim IsRental As Boolean = RadioRental.IsChecked = True

        IsDownloading = True
        IsCanceled = False
        BtnStart.IsEnabled = False
        BtnStart.Opacity = 0.4
        BtnStop.Visibility = Visibility.Visible
        ProgressBar.Value = 0
        LabStatus.Text = "准备中…"
        LabLog.Text = ""
        AppendLog("开始下载网易中国版客户端（版本 " & VersionName & "，类型 " & If(IsRental, "租赁服", "网络服") & "）…")

        RunInNewThread(Sub()
            Try
                ModNeteaseDownload.InstallCNClient(McVersion, VersionName, IsRental,
                    Sub(p As Integer, m As String)
                        RunInUi(Sub()
                            ProgressBar.Value = p
                            LabStatus.Text = m
                            AppendLog(m)
                        End Sub)
                    End Sub,
                    Function() IsCanceled)
            Catch ex As Exception
                RunInUi(Sub()
                    LabStatus.Text = "下载失败：" & ex.Message
                    AppendLog("下载失败：" & ex.Message)
                End Sub)
            Finally
                RunInUi(Sub()
                    IsDownloading = False
                    BtnStart.IsEnabled = True
                    BtnStart.Opacity = 1
                    BtnStop.Visibility = Visibility.Collapsed
                    RefreshLoginState()
                End Sub)
            End Try
        End Sub, "CN Download")
    End Sub

    Private Sub AppendLog(ByVal Text As String)
        Dim Old As String = LabLog.Text
        If Old.Length > 3000 Then
            ' 保留最后 40 行
            Dim Lines As String() = Old.Split(vbCrLf)
            Dim Keep As Integer = Math.Min(Lines.Length, 40)
            Old = String.Join(vbCrLf, Lines.Skip(Lines.Length - Keep).ToArray())
        End If
        LabLog.Text = Old & If(Old = "", "", vbCrLf) & Text
    End Sub

End Class
