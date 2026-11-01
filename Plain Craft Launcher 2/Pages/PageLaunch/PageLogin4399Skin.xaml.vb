Public Class PageLogin4399Skin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.Skin4399
    End Sub
    Private Sub PageLogin4399Skin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Settings.Get(Of String)("Cache4399Name")
        TextEmail.Text = Settings.Get(Of String)("Cache4399Username")
        TextEmail.Visibility = (Not Settings.Get(Of Boolean)("UiLauncherEmail")).ToVisibility
        '皮肤在 Loaded 加载
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Return New McLoginServer(McLoginType.Pt4399) With {.Token = "4399", .UserName = Settings.Get(Of String)("Cache4399Username"), .Password = Settings.Get(Of String)("Cache4399Pass"), .Description = "4399", .Type = McLoginType.Pt4399, .BaseUrl = "4399"}
    End Function

    Private Sub PageLogin4399Skin_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart({
                 AaOpacity(BtnExit, 1 - BtnExit.Opacity, 80),
                 AaHeight(BtnExit, 25.5 - BtnExit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnExit, -1.5, 50, 140, New AniEaseInFluent)
        }, "PageLogin4399Skin Button")
    End Sub
    Private Sub PageLogin4399Skin_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanData.MouseLeave
        AniStart({
                 AaOpacity(BtnExit, -BtnExit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnExit, 14 - BtnExit.Height, 120,, New AniEaseInFluent)
        }, "PageLogin4399Skin Button")
    End Sub

    Public Shared Sub ExitLogin() Handles BtnExit.Click
        Settings.Set("Cache4399Access", "")
        McLogin4399Loader.Input = Nothing '防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

End Class
