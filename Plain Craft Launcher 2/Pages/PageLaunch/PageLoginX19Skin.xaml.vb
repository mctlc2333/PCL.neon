Public Class PageLoginX19Skin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinX19
    End Sub
    Private Sub PageLoginX19Skin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Settings.Get(Of String)("CacheX19Name")
        TextEmail.Text = Settings.Get(Of String)("CacheX19Username")
        TextEmail.Visibility = (Not Settings.Get(Of Boolean)("UiLauncherEmail")).ToVisibility
        '皮肤在 Loaded 加载
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Return New McLoginServer(McLoginType.X19) With {.Token = "X19", .UserName = Settings.Get(Of String)("CacheX19Username"), .Password = Settings.Get(Of String)("CacheX19Pass"), .Description = "网易中国版", .Type = McLoginType.X19, .BaseUrl = "x19"}
    End Function

    Private Sub PageLoginX19Skin_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart({
                 AaOpacity(BtnExit, 1 - BtnExit.Opacity, 80),
                 AaHeight(BtnExit, 25.5 - BtnExit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnExit, -1.5, 50, 140, New AniEaseInFluent)
        }, "PageLoginX19Skin Button")
    End Sub
    Private Sub PageLoginX19Skin_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanData.MouseLeave
        AniStart({
                 AaOpacity(BtnExit, -BtnExit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnExit, 14 - BtnExit.Height, 120,, New AniEaseInFluent)
        }, "PageLoginX19Skin Button")
    End Sub

    Public Shared Sub ExitLogin() Handles BtnExit.Click
        Settings.Set("CacheX19Access", "")
        McLoginX19Loader.Input = Nothing '防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

End Class
