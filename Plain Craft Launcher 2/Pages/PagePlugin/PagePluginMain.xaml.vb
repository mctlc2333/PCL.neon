Public Class PagePluginMain

    ''' <summary>
    ''' 页面加载完成时刷新插件列表。
    ''' </summary>
    Private Sub PagePluginMain_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ModPlugins.ScanPluginsIfNeeded()
        LabHeaderDir.Text = "插件文件夹：" & ModPlugins.PluginsDir()
        RebuildList()
        AddHandler ItemBack.Click, AddressOf BackToList
        AddHandler ItemBack.MouseLeftButtonUp, AddressOf ItemBack_MouseLeftButtonUp
    End Sub

    Private Sub ItemBack_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        'MyListItem 已自带点击逻辑，此处仅作为兼容性保留，防止点击穿透
    End Sub

    ''' <summary>
    ''' 重建插件列表 UI。
    ''' </summary>
    Public Sub RebuildList()
        Try
            PanList.Children.Clear()
            If ModPlugins.PluginList.Count = 0 Then
                PanList.Children.Add(New TextBlock With {
                    .Text = "未发现插件。将插件 DLL 放入『" & ModPlugins.PluginsDir() & "』后点击下方按钮刷新。",
                    .Margin = New Thickness(12, 20, 12, 0),
                    .TextWrapping = TextWrapping.Wrap,
                    .FontSize = 14,
                    .Foreground = TryCast(Application.Current.TryFindResource("ColorBrushGray3"), Brush)
                })
            End If
            For Each Info As PclPluginInfo In ModPlugins.PluginList
                PanList.Children.Add(BuildPluginCard(Info))
            Next

            '刷新按钮
            Dim ItemRefresh As New MyListItem With {
                .Type = MyListItem.CheckType.Clickable,
                .Title = "重新扫描插件",
                .Height = 36,
                .Margin = New Thickness(0, 4, 0, 0),
                .IsScaleAnimationEnabled = False,
                .ToolTip = "重新扫描 plugins 文件夹并加载所有插件",
                .LogoScale = 0.9,
                .Logo = "M942.578125 468.671875c-4.296875-12.1875-16.09375-20.3125-28.90625-19.921875L849.921875 451.5c7.34375-25.21875 10.78125-51.390625 10.78125-77.9375 0-12.40625-1.046875-24.96875-3.171875-37.296875l59.96875-15.421875c12.90625-3.296875 23.515625-12.453125 26.9375-25.265625s-0.75-26.453125-10.296875-34.890625l-20.59375-18.21875c-9.546875-8.4375-23.109375-10.265625-34.546875-4.625l-55.734375 27.53125c-16.640625-20.1875-36.90625-37.5-59.890625-51.109375l16.703125-58.640625c3.609375-12.640625 0.125-26.0625-8.859375-35.5-8.984375-9.4375-22.234375-13.203125-34.875-9.890625l-27.28125 7.09375c-12.625 3.296875-22.53125 12.484375-26.125 25.125l-17.625 62.265625c-25.75-2.296875-51.75-0.5-76.25 5.203125l-26.453125-55.78125c-6.140625-12.921875-17.640625-22.234375-31.078125-25.15625-13.421875-2.921875-27.390625 0.359375-37.65625 8.828125l-22.171875 18.296875c-10.234375 8.484375-15.140625 21.71875-13.203125 35.375l8.96875 62.375c-23.53125 10.140625-44.4375 24.640625-61.765625 42.453125l-59.421875-24.8125c-12.703125-5.28125-27.203125-3.765625-38.484375 3.96875-11.265625 7.75-18.3125 20.109375-18.65625 33.671875l-0.59375 28.0625c-0.296875 13.5625 6.203125 26.21875 17.171875 34.53125l55.140625 40.828125c-0.78125 12.921875-0.640625 25.8125 0.53125 38.375l-56.296875 35.390625c-12.328125 7.75-19.4375 21.296875-18.671875 35.421875 0.78125 14.140625 9.203125 26.890625 22.203125 33.484375l26.609375 13.5c12.984375 6.609375 28.59375 6.109375 39.0625-1.109375l50.15625-34.578125c19.40625 16.15625 42.234375 28.515625 67.03125 36.3125l4.421875 62.71875c0.921875 13.40625 8.546875 25.09375 20.140625 31.28125 4.828125 2.578125 10.265625 3.921875 15.71875 3.921875 7.765625 0 15.515625-2.421875 21.953125-7.015625l25.046875-17.921875c11.09375-7.953125 17.875-20.703125 17.796875-34.140625l-0.375-65.8125c6.25-0.9375 12.390625-1.40625 18.5-1.40625 6.140625 0 12.3125 0.46875 18.5625 1.40625l-0.375 65.84375c-0.078125 13.4375 6.703125 26.1875 17.796875 34.140625l25.046875 17.921875c6.4375 4.609375 14.1875 7.03125 21.953125 7.015625 5.453125 0 10.890625-1.34375 15.71875-3.921875 11.578125-6.1875 19.21875-17.875 20.125-31.28125l4.421875-62.71875c24.796875-7.796875 47.625-20.15625 67.03125-36.3125l50.140625 34.578125c10.484375 7.21875 26.09375 7.71875 39.078125 1.109375l26.609375-13.5c13-6.609375 21.421875-19.34375 22.203125-33.484375 0.78125-14.125-6.34375-27.671875-18.671875-35.421875l-56.296875-35.40625c1.171875-12.546875 1.3125-25.4375 0.53125-38.359375l55.140625-40.84375c10.96875-8.3125 17.46875-20.96875 17.171875-34.53125-0.0625-0.28125-0.109375-0.546875-0.15625-0.8125zM505.75 626.5c-76.375 0-138.34375-61.96875-138.34375-138.34375s61.96875-138.34375 138.34375-138.34375 138.34375 61.96875 138.34375 138.34375-61.96875 138.34375-138.34375 138.34375z"
            }
            PanList.Children.Add(ItemRefresh)
            AddHandler ItemRefresh.Click, AddressOf RefreshPlugins
        Catch ex As Exception
            Logger.Error(ex, "加载插件列表 UI 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 构建单个插件的卡片。
    ''' </summary>
    Private Function BuildPluginCard(Info As PclPluginInfo) As FrameworkElement
        Dim Title As String = If(String.IsNullOrEmpty(Info.Name), System.IO.Path.GetFileName(Info.FilePath), Info.Name)
        If Not String.IsNullOrEmpty(Info.Version) Then Title &= "  v" & Info.Version

        Dim Card As New MyCard With {.Title = Title, .Margin = New Thickness(0, 0, 0, 12)}

        '内容加入 MyCard 的内容区（MainGrid，即 Children(2)），避免与左上角标题重叠
        Dim ContentPanel As Panel = TryCast(Card.Children(2), Panel)
        If ContentPanel Is Nothing Then Return Card
        Dim Stack As New StackPanel With {.Margin = New Thickness(20, 42, 18, 12)}
        ContentPanel.Children.Add(Stack)

        '作者 / 状态行
        Dim Line1 As String = ""
        If Not String.IsNullOrEmpty(Info.Author) Then Line1 &= "作者：" & Info.Author & "    "
        Line1 &= If(Info.Loaded, "状态：已加载", "状态：加载失败")
        Stack.Children.Add(New TextBlock With {
            .Text = Line1,
            .FontSize = 13,
            .Margin = New Thickness(0, 0, 0, 4),
            .Foreground = If(Info.Loaded, TryCast(Application.Current.TryFindResource("ColorBrushGray2"), Brush), Brushes.IndianRed)
        })

        '描述
        If Not String.IsNullOrEmpty(Info.Description) Then
            Stack.Children.Add(New TextBlock With {
                .Text = Info.Description,
                .FontSize = 13,
                .LineHeight = 18,
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 0, 0, 8),
                .Foreground = TryCast(Application.Current.TryFindResource("ColorBrushGray3"), Brush)
            })
        End If

        '错误信息
        If Not Info.Loaded OrElse Not String.IsNullOrEmpty(Info.ErrorMessage) Then
            Stack.Children.Add(New TextBlock With {
                .Text = Info.ErrorMessage,
                .FontSize = 12,
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 0, 0, 8),
                .Foreground = Brushes.IndianRed
            })
        End If

        '打开页面入口（仅当插件提供 GetPage 时显示）
        If Info.Loaded AndAlso Info.Instance IsNot Nothing Then
            Dim HasPage As Boolean = False
            Try
                HasPage = Info.Instance.GetType().GetMethod("GetPage") IsNot Nothing
            Catch ex As Exception
                HasPage = False
            End Try
            If HasPage Then
                Dim ItemOpen As New MyListItem With {
                    .Type = MyListItem.CheckType.Clickable,
                    .Title = "打开页面",
                    .Height = 32,
                    .IsScaleAnimationEnabled = False,
                    .LogoScale = 0.75,
                    .Logo = "M615.7 392.6L290 717.3c-32.4 32.4-85.1 32.4-117.5 0-32.4-32.4-32.4-85.1 0-117.5l343.1-343.1c46.3-46.3 121.4-46.3 167.7 0 46.3 46.3 46.3 121.4 0 167.7L339 625.5c-15.5 15.5-40.6 15.5-56.1 0-15.5-15.5-15.5-40.6 0-56.1l284.2-284.2c3.1-3.1 3.1-8.1 0-11.2-3.1-3.1-8.1-3.1-11.2 0L272.7 555.2c-26 26-26 68.1 0 94.1 26 26 68.1 26 94.1 0l344.4-344.4c15.4-15.4 15.4-40.3 0-55.7-15.4-15.4-40.3-15.4-55.7 0l-343.6 343.6c-41.8 41.8-109.5 41.8-151.3 0-41.8-41.8-41.8-109.5 0-151.3L602.5 150.2c72.1-72.1 189-72.1 261.1 0 72.1 72.1 72.1 189 0 261.1L615.7 655.9c-50.1 50.1-131.3 50.1-181.4 0-50.1-50.1-50.1-131.3 0-181.4l296.1-296.1c3.1-3.1 3.1-8.1 0-11.2-3.1-3.1-8.1-3.1-11.2 0l-296.1 296.1c-39 39-39 102.2 0 141.2 39 39 102.2 39 141.2 0l325.3-325.3c56.1-56.1 56.1-147.1 0-203.2-56.1-56.1-147.1-56.1-203.2 0L409.5 389.4c-62.3 62.3-62.3 163.3 0 225.6 62.3 62.3 163.3 62.3 225.6 0l343.3-343.3c26-26 26-68.1 0-94.1-26-26-68.1-26-94.1 0L431.3 581.5c-11.9 11.9-11.9 31.3 0 43.2 11.9 11.9 31.3 11.9 43.2 0L676.2 423c42.5-42.5 42.5-111.5 0-154.1-42.5-42.5-111.5-42.5-154.1 0L322.4 468.6c-54 54-54 141.5 0 195.5 54 54 141.5 54 195.5 0l325.3-325.3"
                }
                Dim Clicked As PclPluginInfo = Info
                AddHandler ItemOpen.Click, Sub() OpenPluginPage(Clicked)
                Stack.Children.Add(ItemOpen)
            End If
        End If

        Return Card
    End Function

    ''' <summary>
    ''' 打开指定插件的页面。
    ''' </summary>
    Private Sub OpenPluginPage(Info As PclPluginInfo)
        Try
            Dim Page As FrameworkElement = ModPlugins.GetPluginPage(Info)
            If Page Is Nothing Then
                Hint("该插件未能提供可用的页面", HintType.Red)
                Return
            End If
            LabHostTitle.Text = "插件页面：" & Info.Name
            PanHostContent.Children.Clear()
            PanHostContent.Children.Add(Page)
            PanListRoot.Visibility = Visibility.Collapsed
            PanHostRoot.Visibility = Visibility.Visible
        Catch ex As Exception
            Logger.Error(ex, "打开插件页面失败")
            Hint("打开插件页面失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ''' <summary>
    ''' 返回插件列表。
    ''' </summary>
    Private Sub BackToList(sender As Object, e As RoutedEventArgs)
        PanHostRoot.Visibility = Visibility.Collapsed
        PanListRoot.Visibility = Visibility.Visible
        PanHostContent.Children.Clear()
    End Sub

    ''' <summary>
    ''' 重新扫描插件。
    ''' </summary>
    Private Sub RefreshPlugins(sender As Object, e As RoutedEventArgs)
        ModPlugins.ScanPlugins()
        RebuildList()
        Hint("插件已重新扫描", HintType.Green)
    End Sub

End Class
