'ModPlugins.vb —— 分支新增：插件加载模块
'实现说明：
' 1. 扫描 exe 同目录 plugins\ 文件夹下的 *.dll，作为插件加载。
' 2. 插件采用『约定 + 反射』的鸭子类型接口，无需引用启动器程序集即可开发：
'    - 入口类：名称以 Plugin 结尾或为 Main，且具有无参构造函数；
'    - 可选属性：Name / Version / Author / Description（String）；
'    - 可选方法：OnLoad()（加载时调用）、OnUnload()（卸载时调用，暂未使用）、
'      GetPage()（返回一个 FrameworkElement 作为插件页面）。
'    - 上述方法可带一个参数（OnLoad(api) / GetPage(api)）：参数为插件宿主
'      PluginApi 实例，插件可借此调用启动器能力（提示、日志、语言、设置读写、
'      页面跳转等），从而实现『插件修改启动器功能』。参数声明为 Object 即可，
'      无需引用启动器程序集（VB 晚期绑定）。
' 3. 单个插件加载失败不会影响启动器与其它插件。

Imports System.IO
Imports System.Reflection
Imports System.Windows

''' <summary>
''' 单个插件的运行时信息。
''' </summary>
Public Class PclPluginInfo

    ''' <summary>
    ''' 插件 DLL 的完整路径。
    ''' </summary>
    Public FilePath As String = ""

    ''' <summary>
    ''' 插件入口实例。
    ''' </summary>
    Public Instance As Object = Nothing

    ''' <summary>
    ''' 插件名称（约定属性 Name，缺省显示文件名）。
    ''' </summary>
    Public Name As String = ""

    ''' <summary>
    ''' 插件版本（约定属性 Version）。
    ''' </summary>
    Public Version As String = ""

    ''' <summary>
    ''' 插件作者（约定属性 Author）。
    ''' </summary>
    Public Author As String = ""

    ''' <summary>
    ''' 插件描述（约定属性 Description）。
    ''' </summary>
    Public Description As String = ""

    ''' <summary>
    ''' 是否成功加载（含 OnLoad 成功）。
    ''' </summary>
    Public Loaded As Boolean = False

    ''' <summary>
    ''' 加载/运行错误信息（加载失败或运行时出错时记录）。
    ''' </summary>
    Public ErrorMessage As String = ""

End Class

Public Module ModPlugins

    ''' <summary>
    ''' 已扫描到的插件列表。
    ''' </summary>
    Public PluginList As New List(Of PclPluginInfo)

    Private HasScanned As Boolean = False

    ''' <summary>
    ''' 插件文件夹路径（exe 同目录 plugins）。
    ''' </summary>
    Public Function PluginsDir() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins")
    End Function

    ''' <summary>
    ''' 扫描插件文件夹并加载全部插件。
    ''' </summary>
    Public Sub ScanPlugins()
        PluginList.Clear()
        HasScanned = True

        '确保插件文件夹存在（不存在也无需报错）
        Try
            If Not Directory.Exists(PluginsDir()) Then Directory.CreateDirectory(PluginsDir())
        Catch ex As Exception
            Logger.Warn("创建插件文件夹失败：" & ex.Message)
        End Try

        Dim Files As String() = {}
        Try
            Files = Directory.GetFiles(PluginsDir(), "*.dll")
        Catch ex As Exception
            Logger.Warn("读取插件文件夹失败：" & ex.Message)
        End Try

        For Each File As String In Files
            Dim Info As New PclPluginInfo With {.FilePath = File}
            Try
                Dim Assembly As Assembly = Assembly.LoadFrom(File)
                '跳过启动器自身的程序集
                If Assembly.GetName().Name.IndexOf("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    Info.ErrorMessage = "忽略启动器自身程序集"
                    PluginList.Add(Info)
                    Continue For
                End If

                Dim EntryFound As Boolean = False
                For Each Type As Type In Assembly.GetTypes()
                    If Not Type.IsClass Then Continue For
                    If Type.IsAbstract Then Continue For
                    '约定：入口类名以 Plugin 结尾，或为 Main，且必须有无参构造函数
                    If Not (Type.Name.EndsWith("Plugin") OrElse Type.Name = "Main") Then Continue For
                    If Type.GetConstructor(Type.EmptyTypes) Is Nothing Then Continue For

                    Dim Instance As Object = Activator.CreateInstance(Type)
                    Info.Instance = Instance
                    Info.Name = ReadProperty(Instance, "Name", "")
                    Info.Version = ReadProperty(Instance, "Version", "")
                    Info.Author = ReadProperty(Instance, "Author", "")
                    Info.Description = ReadProperty(Instance, "Description", "")

                    '调用 OnLoad（失败只记录，不阻塞其它插件；支持 OnLoad(api)）
                    Try
                        InvokeMethod(Instance, "OnLoad", New PluginApi)
                    Catch ex As Exception
                        Info.ErrorMessage = "OnLoad 调用失败：" & ex.Message
                    End Try

                    Info.Loaded = True
                    EntryFound = True
                    Exit For
                Next

                If Not EntryFound Then
                    Info.ErrorMessage = "未找到插件入口类（类名需以 Plugin 结尾或为 Main，且需有无参构造函数）"
                End If
            Catch ex As Exception
                Info.ErrorMessage = "加载失败：" & ex.Message
            End Try
            PluginList.Add(Info)
        Next
    End Sub

    ''' <summary>
    ''' 仅首次调用时扫描（供插件页打开时使用，避免重复扫描）。
    ''' </summary>
    Public Sub ScanPluginsIfNeeded()
        If Not HasScanned Then ScanPlugins()
    End Sub

    ''' <summary>
    ''' 调用约定的方法（无参或带插件宿主参数 api；方法不存在时静默跳过）。
    ''' </summary>
    Private Sub InvokeMethod(Instance As Object, Name As String, Optional Api As PluginApi = Nothing)
        Dim Method As MethodInfo = Instance.GetType().GetMethod(Name)
        If Method Is Nothing Then Return
        If Method.GetParameters().Length = 0 Then
            Method.Invoke(Instance, Nothing)
        Else
            Method.Invoke(Instance, {Api})
        End If
    End Sub

    ''' <summary>
    ''' 获取插件页面（约定方法 GetPage，支持无参或带宿主参数 api；
    ''' 返回 FrameworkElement，失败返回 Nothing）。
    ''' </summary>
    Public Function GetPluginPage(Info As PclPluginInfo) As FrameworkElement
        If Info Is Nothing OrElse Info.Instance Is Nothing Then Return Nothing
        Try
            Dim Method As MethodInfo = Info.Instance.GetType().GetMethod("GetPage")
            If Method Is Nothing Then Return Nothing
            Dim Result As Object
            If Method.GetParameters().Length = 0 Then
                Result = Method.Invoke(Info.Instance, Nothing)
            Else
                Result = Method.Invoke(Info.Instance, {New PluginApi})
            End If
            Return TryCast(Result, FrameworkElement)
        Catch ex As Exception
            Info.ErrorMessage = "获取插件页面失败：" & ex.Message
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 读取约定的字符串属性，失败或缺省时返回默认值。
    ''' </summary>
    Private Function ReadProperty(Instance As Object, Name As String, DefaultValue As String) As String
        Try
            Dim PropertyInfo As PropertyInfo = Instance.GetType().GetProperty(Name)
            If PropertyInfo IsNot Nothing AndAlso PropertyInfo.CanRead Then
                Dim Value As Object = PropertyInfo.GetValue(Instance, Nothing)
                If Value IsNot Nothing Then Return Value.ToString()
            End If
        Catch ex As Exception
            '忽略
        End Try
        Return DefaultValue
    End Function

End Module

''' <summary>
''' 插件宿主 API：插件通过 OnLoad(api) / GetPage(api) 获得该对象，
''' 用于调用启动器能力（提示、日志、语言、设置读写、页面跳转等），
''' 从而『修改启动器功能』。插件无需引用启动器程序集：
''' 将参数声明为 Object 并用 VB 晚期绑定调用（Option Strict Off）即可。
''' </summary>
Public Class PluginApi

    ''' <summary>
    ''' 启动器主窗口（FormMain）。插件可通过它访问启动器的公开成员。
    ''' </summary>
    Public ReadOnly Property MainWindow As Object
        Get
            Return ModMain.FrmMain
        End Get
    End Property

    ''' <summary>
    ''' 在启动器左下角弹出提示条。
    ''' Type：0=蓝色（默认） 1=绿色 2=红色。
    ''' </summary>
    Public Sub Hint(Text As String, Optional Type As Integer = 0)
        If Type < 0 Then Type = 0
        If Type > 2 Then Type = 2
        ModMain.Hint(Text, CType(Type, ModMain.HintType))
    End Sub

    ''' <summary>
    ''' 写入启动器日志（Log 文件夹）。
    ''' </summary>
    Public Sub Log(Text As String)
        Logger.Info(Text)
    End Sub

    ''' <summary>
    ''' 记录插件异常到启动器日志。
    ''' </summary>
    Public Sub LogError(Ex As Exception, Optional Tag As String = "")
        Logger.Error(Ex, If(Tag = "", "Plugin", Tag))
    End Sub

    ''' <summary>
    ''' 当前语言的翻译（与启动器界面同源；未收录词条返回原文）。
    ''' </summary>
    Public Function Lang(Source As String) As String
        Return ModLanguage.Lang(Source)
    End Function

    ''' <summary>
    ''' 读取启动器设置项（注册表持久化；不存在时返回 Nothing）。
    ''' </summary>
    Public Function GetSetting(Key As String) As Object
        Try
            Return Settings.GetSafe(Key)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 写入启动器设置项（持久化到注册表）。
    ''' </summary>
    Public Sub SetSetting(Key As String, Value As Object)
        Settings.SetSafe(Key, Value)
    End Sub

    ''' <summary>
    ''' 切换启动器页面。PageId：0=启动 1=下载 2=联机 3=设置 4=更多 10=插件。
    ''' </summary>
    Public Sub OpenPage(PageId As Integer)
        If ModMain.FrmMain Is Nothing Then Return
        Dim Stack As New FormMain.PageStackData
        Stack.Page = CType(PageId, FormMain.PageType)
        ModMain.FrmMain.PageChange(Stack)
    End Sub

    ''' <summary>
    ''' 启动器 exe 所在目录。
    ''' </summary>
    Public ReadOnly Property WorkingDirectory As String
        Get
            Return AppDomain.CurrentDomain.BaseDirectory
        End Get
    End Property

    ''' <summary>
    ''' 插件文件夹路径。
    ''' </summary>
    Public ReadOnly Property PluginsDirectory As String
        Get
            Return ModPlugins.PluginsDir()
        End Get
    End Property

End Class
