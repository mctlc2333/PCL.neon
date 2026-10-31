'ModLanguage.vb —— 分支新增：多语言切换模块（73 种语言）
'实现说明：
' 1. 在 设置 → 系统 → 全局设置 中提供 73 种语言下拉框，选择后立即生效并持久化（设置项 UiLanguage）。
' 2. 翻译采用“翻译表 + 中文兜底”策略：已收录的 UI 字符串按当前语言翻译；未收录的字符串保持中文。
' 3. 主界面顶部导航（启动/下载/联机/设置/更多）在 73 种语言下均有翻译；设置页标签与常见按钮在主要语言下有翻译。
' 4. 更换语言时遍历当前可见 UI 子树即时应用；重启后按已保存语言自动应用。

Public Structure LanguageEntry
    Public Code As String
    Public Name As String
    Public Sub New(Code As String, Name As String)
        Me.Code = Code
        Me.Name = Name
    End Sub
End Structure

Public Module ModLanguage

    ''' <summary>
    ''' 支持的语言数量（73 种）。
    ''' </summary>
    Public Const LanguageCount As Integer = 73

    ''' <summary>
    ''' 语言列表（代码 + 显示名称），索引即设置项 UiLanguage 存储的数值。
    ''' </summary>
    Public ReadOnly LanguageList As LanguageEntry() = {
        New LanguageEntry("zh_CN", "简体中文（默认）"),
        New LanguageEntry("zh_HK", "繁體中文（香港）"),
        New LanguageEntry("zh_TW", "繁體中文（台灣）"),
        New LanguageEntry("zh_MO", "繁體中文（澳門）"),
        New LanguageEntry("en_US", "English (US)"),
        New LanguageEntry("en_GB", "English (UK)"),
        New LanguageEntry("en_AU", "English (Australia)"),
        New LanguageEntry("ja_JP", "日本語"),
        New LanguageEntry("ko_KR", "한국어"),
        New LanguageEntry("fr_FR", "Français"),
        New LanguageEntry("de_DE", "Deutsch"),
        New LanguageEntry("es_ES", "Español"),
        New LanguageEntry("es_MX", "Español (México)"),
        New LanguageEntry("pt_PT", "Português"),
        New LanguageEntry("pt_BR", "Português (Brasil)"),
        New LanguageEntry("ru_RU", "Русский"),
        New LanguageEntry("it_IT", "Italiano"),
        New LanguageEntry("nl_NL", "Nederlands"),
        New LanguageEntry("pl_PL", "Polski"),
        New LanguageEntry("tr_TR", "Türkçe"),
        New LanguageEntry("uk_UA", "Українська"),
        New LanguageEntry("ar_SA", "العربية"),
        New LanguageEntry("ar_EG", "العربية (مصر)"),
        New LanguageEntry("he_IL", "עברית"),
        New LanguageEntry("th_TH", "ไทย"),
        New LanguageEntry("vi_VN", "Tiếng Việt"),
        New LanguageEntry("id_ID", "Bahasa Indonesia"),
        New LanguageEntry("ms_MY", "Bahasa Melayu"),
        New LanguageEntry("fil_PH", "Filipino"),
        New LanguageEntry("hi_IN", "हिन्दी"),
        New LanguageEntry("bn_BD", "বাংলা"),
        New LanguageEntry("ta_IN", "தமிழ்"),
        New LanguageEntry("te_IN", "తెలుగు"),
        New LanguageEntry("mr_IN", "मराठी"),
        New LanguageEntry("gu_IN", "ગુજરાતી"),
        New LanguageEntry("kn_IN", "ಕನ್ನಡ"),
        New LanguageEntry("ml_IN", "മലയാളം"),
        New LanguageEntry("pa_IN", "ਪੰਜਾਬੀ"),
        New LanguageEntry("ne_NP", "नेपाली"),
        New LanguageEntry("si_LK", "සිංහල"),
        New LanguageEntry("ur_PK", "اردو"),
        New LanguageEntry("fa_IR", "فارسی"),
        New LanguageEntry("cs_CZ", "Čeština"),
        New LanguageEntry("sk_SK", "Slovenčina"),
        New LanguageEntry("hu_HU", "Magyar"),
        New LanguageEntry("ro_RO", "Română"),
        New LanguageEntry("bg_BG", "Български"),
        New LanguageEntry("hr_HR", "Hrvatski"),
        New LanguageEntry("sr_RS", "Српски"),
        New LanguageEntry("sl_SI", "Slovenščina"),
        New LanguageEntry("lt_LT", "Lietuvių"),
        New LanguageEntry("lv_LV", "Latviešu"),
        New LanguageEntry("et_EE", "Eesti"),
        New LanguageEntry("el_GR", "Ελληνικά"),
        New LanguageEntry("da_DK", "Dansk"),
        New LanguageEntry("sv_SE", "Svenska"),
        New LanguageEntry("nb_NO", "Norsk Bokmål"),
        New LanguageEntry("fi_FI", "Suomi"),
        New LanguageEntry("is_IS", "Íslenska"),
        New LanguageEntry("ga_IE", "Gaeilge"),
        New LanguageEntry("cy_GB", "Cymraeg"),
        New LanguageEntry("eu_ES", "Euskara"),
        New LanguageEntry("ca_ES", "Català"),
        New LanguageEntry("gl_ES", "Galego"),
        New LanguageEntry("az_AZ", "Azərbaycan"),
        New LanguageEntry("kk_KZ", "Қазақ"),
        New LanguageEntry("uz_UZ", "Oʻzbek"),
        New LanguageEntry("hy_AM", "Հայերեն"),
        New LanguageEntry("ka_GE", "ქართული"),
        New LanguageEntry("sw_KE", "Kiswahili"),
        New LanguageEntry("af_ZA", "Afrikaans"),
        New LanguageEntry("sq_AL", "Shqip"),
        New LanguageEntry("mk_MK", "Македонски")
    }

    ''' <summary>
    ''' 当前语言代码。
    ''' </summary>
    Public ReadOnly Property LanguageCurrent As String
        Get
            Dim Index As Integer = 0
            Try
                Index = CInt(Settings.Get("UiLanguage"))
            Catch ex As Exception
                Index = 0
            End Try
            If Index < 0 OrElse Index >= LanguageList.Length Then Index = 0
            Return LanguageList(Index).Code
        End Get
    End Property

    ''' <summary>
    ''' 翻译指定字符串；未收录时回退英文，再回退原文（中文）。
    ''' </summary>
    Public Function Lang(Source As String) As String
        If String.IsNullOrEmpty(Source) Then Return Source
        Dim Table As Dictionary(Of String, String) = Nothing
        Dim Result As String = Nothing
        If Translations.TryGetValue(LanguageCurrent, Table) Then
            If Table.TryGetValue(Source, Result) Then Return Result
        End If
        If LanguageCurrent <> "en_US" AndAlso Translations.TryGetValue("en_US", Table) Then
            If Table.TryGetValue(Source, Result) Then Return Result
        End If
        Return Source
    End Function

    ''' <summary>
    ''' 应用当前语言到已知 UI（主界面顶部导航 + 设置页）。
    ''' 由设置项 UiLanguage 的变更事件调用，也在主窗口加载完成后调用一次。
    ''' </summary>
    Public Sub LangApply(Optional Value As Object = Nothing)
        Try
            If FrmMain IsNot Nothing Then LangApplySubTree(FrmMain.PanTitle)
            If FrmSetupSystem IsNot Nothing Then LangApplySubTree(FrmSetupSystem.PanMain)
        Catch ex As Exception
            Logger.Error(ex, "应用语言失败")
        End Try
    End Sub

    Private Sub LangApplySubTree(Root As DependencyObject)
        For Each Child As DependencyObject In LogicalTreeHelper.GetChildren(Root)
            If TypeOf Child Is TextBlock Then
                DirectCast(Child, TextBlock).Text = Lang(DirectCast(Child, TextBlock).Text)
            ElseIf TypeOf Child Is MyRadioButton Then
                DirectCast(Child, MyRadioButton).Text = Lang(DirectCast(Child, MyRadioButton).Text)
            ElseIf TypeOf Child Is MyCheckBox Then
                DirectCast(Child, MyCheckBox).Text = Lang(DirectCast(Child, MyCheckBox).Text)
            ElseIf TypeOf Child Is MyButton Then
                DirectCast(Child, MyButton).Text = Lang(DirectCast(Child, MyButton).Text)
            ElseIf TypeOf Child Is MyCard Then
                DirectCast(Child, MyCard).Title = Lang(DirectCast(Child, MyCard).Title)
            End If
            LangApplySubTree(Child)
        Next
    End Sub

    '翻译表：外层键为语言代码，内层键为中文源字符串。
    Private ReadOnly Translations As Dictionary(Of String, Dictionary(Of String, String)) = New Dictionary(Of String, Dictionary(Of String, String))() From {
        {"zh_CN", New Dictionary(Of String, String)() From {}}, '中文为默认语言，无需翻译
        {"zh_HK", New Dictionary(Of String, String)() From {
            {"启动", "啟動"}, {"下载", "下載"}, {"联机", "聯機"}, {"设置", "設定"}, {"更多", "更多"},
            {"全局设置", "全局設定"}, {"语言", "語言"}, {"社区资源", "社群資源"}, {"辅助功能", "輔助功能"},
            {"启动器", "啟動器"}, {"调试选项", "偵錯選項"}, {"文件下载源", "檔案下載來源"}, {"版本列表源", "版本列表來源"},
            {"最大线程数", "最大執行緒數"}, {"速度限制", "速度限制"}, {"目标文件夹", "目標資料夾"}, {"游戏更新提示", "遊戲更新提示"},
            {"正式版更新提示", "正式版更新提示"}, {"测试版更新提示", "測試版更新提示"}, {"游戏语言", "遊戲語言"},
            {"自动设置为中文", "自動設為中文"}, {"启动器更新", "啟動器更新"}, {"启动器公告", "啟動器公告"},
            {"缓存文件夹", "快取資料夾"}, {"匿名数据收集", "匿名資料收集"}, {"检查更新", "檢查更新"},
            {"导出设置", "匯出設定"}, {"导入设置", "匯入設定"}, {"确定", "確定"}, {"取消", "取消"},
            {"警告", "警告"}, {"我知道了", "我知道了"}, {"正在刷新……", "正在重新整理……"}, {"开始", "開始"},
            {"关闭", "關閉"}, {"版本", "版本"}, {"整合包", "整合包"}
        }},
        {"zh_TW", New Dictionary(Of String, String)() From {
            {"启动", "啟動"}, {"下载", "下載"}, {"联机", "連線"}, {"设置", "設定"}, {"更多", "更多"},
            {"全局设置", "全域設定"}, {"语言", "語言"}, {"社区资源", "社群資源"}, {"辅助功能", "輔助功能"},
            {"启动器", "啟動器"}, {"调试选项", "偵錯選項"}, {"文件下载源", "檔案下載來源"}, {"版本列表源", "版本列表來源"},
            {"最大线程数", "最大執行緒數"}, {"速度限制", "速度限制"}, {"目标文件夹", "目標資料夾"}, {"游戏更新提示", "遊戲更新提示"},
            {"正式版更新提示", "正式版更新提示"}, {"测试版更新提示", "測試版更新提示"}, {"游戏语言", "遊戲語言"},
            {"自动设置为中文", "自動設為中文"}, {"启动器更新", "啟動器更新"}, {"启动器公告", "啟動器公告"},
            {"缓存文件夹", "快取資料夾"}, {"匿名数据收集", "匿名資料收集"}, {"检查更新", "檢查更新"},
            {"导出设置", "匯出設定"}, {"导入设置", "匯入設定"}, {"确定", "確定"}, {"取消", "取消"},
            {"警告", "警告"}, {"我知道了", "我知道了"}, {"正在刷新……", "正在重新整理……"}, {"开始", "開始"},
            {"关闭", "關閉"}, {"版本", "版本"}, {"整合包", "整合包"}
        }},
        {"zh_MO", New Dictionary(Of String, String)() From {
            {"启动", "啟動"}, {"下载", "下載"}, {"联机", "聯機"}, {"设置", "設定"}, {"更多", "更多"}
        }},
        {"en_US", New Dictionary(Of String, String)() From {
            {"启动", "Launch"}, {"下载", "Download"}, {"联机", "Online"}, {"设置", "Settings"}, {"更多", "More"},
            {"全局设置", "Global Settings"}, {"语言", "Language"}, {"社区资源", "Community Resources"}, {"辅助功能", "Accessibility"},
            {"启动器", "Launcher"}, {"调试选项", "Debug Options"}, {"文件下载源", "File Download Source"}, {"版本列表源", "Version List Source"},
            {"最大线程数", "Max Threads"}, {"速度限制", "Speed Limit"}, {"目标文件夹", "Target Folder"}, {"游戏更新提示", "Game Update Notifications"},
            {"正式版更新提示", "Release Update Notifications"}, {"测试版更新提示", "Snapshot Update Notifications"}, {"游戏语言", "Game Language"},
            {"自动设置为中文", "Auto-set to Chinese"}, {"启动器更新", "Launcher Updates"}, {"启动器公告", "Launcher Announcements"},
            {"缓存文件夹", "Cache Folder"}, {"匿名数据收集", "Anonymous Data Collection"}, {"检查更新", "Check for Updates"},
            {"导出设置", "Export Settings"}, {"导入设置", "Import Settings"}, {"确定", "OK"}, {"取消", "Cancel"},
            {"警告", "Warning"}, {"我知道了", "Got it"}, {"正在刷新……", "Refreshing..."}, {"开始", "Start"},
            {"关闭", "Close"}, {"版本", "Version"}, {"整合包", "Modpack"}
        }},
        {"en_GB", New Dictionary(Of String, String)() From {
            {"启动", "Launch"}, {"下载", "Download"}, {"联机", "Online"}, {"设置", "Settings"}, {"更多", "More"},
            {"全局设置", "Global Settings"}, {"语言", "Language"}, {"社区资源", "Community Resources"}, {"辅助功能", "Accessibility"},
            {"启动器", "Launcher"}, {"调试选项", "Debug Options"}, {"文件下载源", "File Download Source"}, {"版本列表源", "Version List Source"},
            {"最大线程数", "Max Threads"}, {"速度限制", "Speed Limit"}, {"目标文件夹", "Target Folder"}, {"游戏更新提示", "Game Update Notifications"},
            {"正式版更新提示", "Release Update Notifications"}, {"测试版更新提示", "Snapshot Update Notifications"}, {"游戏语言", "Game Language"},
            {"自动设置为中文", "Auto-set to Chinese"}, {"启动器更新", "Launcher Updates"}, {"启动器公告", "Launcher Announcements"},
            {"缓存文件夹", "Cache Folder"}, {"匿名数据收集", "Anonymous Data Collection"}, {"检查更新", "Check for Updates"},
            {"导出设置", "Export Settings"}, {"导入设置", "Import Settings"}, {"确定", "OK"}, {"取消", "Cancel"},
            {"警告", "Warning"}, {"我知道了", "Got it"}, {"正在刷新……", "Refreshing..."}, {"开始", "Start"},
            {"关闭", "Close"}, {"版本", "Version"}, {"整合包", "Modpack"}
        }},
        {"en_AU", New Dictionary(Of String, String)() From {
            {"启动", "Launch"}, {"下载", "Download"}, {"联机", "Online"}, {"设置", "Settings"}, {"更多", "More"}
        }},
        {"ja_JP", New Dictionary(Of String, String)() From {
            {"启动", "起動"}, {"下载", "ダウンロード"}, {"联机", "マルチ"}, {"设置", "設定"}, {"更多", "その他"},
            {"全局设置", "全般設定"}, {"语言", "言語"}, {"社区资源", "コミュニティリソース"}, {"辅助功能", "アクセシビリティ"},
            {"启动器", "ランチャー"}, {"调试选项", "デバッグオプション"}, {"文件下载源", "ファイルダウンロード元"}, {"版本列表源", "バージョンリスト元"},
            {"最大线程数", "最大スレッド数"}, {"速度限制", "速度制限"}, {"目标文件夹", "対象フォルダ"}, {"游戏更新提示", "ゲーム更新通知"},
            {"正式版更新提示", "正式版更新通知"}, {"测试版更新提示", "スナップショット更新通知"}, {"游戏语言", "ゲーム言語"},
            {"自动设置为中文", "自動で中国語に設定"}, {"启动器更新", "ランチャー更新"}, {"启动器公告", "ランチャーお知らせ"},
            {"缓存文件夹", "キャッシュフォルダ"}, {"匿名数据收集", "匿名データ収集"}, {"检查更新", "更新を確認"},
            {"导出设置", "設定をエクスポート"}, {"导入设置", "設定をインポート"}, {"确定", "OK"}, {"取消", "キャンセル"},
            {"警告", "警告"}, {"我知道了", "了解"}, {"正在刷新……", "更新中..."}, {"开始", "開始"},
            {"关闭", "閉じる"}, {"版本", "バージョン"}, {"整合包", "統合パック"}
        }},
        {"ko_KR", New Dictionary(Of String, String)() From {
            {"启动", "시작"}, {"下载", "다운로드"}, {"联机", "멀티플레이"}, {"设置", "설정"}, {"更多", "더보기"},
            {"全局设置", "전역 설정"}, {"语言", "언어"}, {"社区资源", "커뮤니티 리소스"}, {"辅助功能", "접근성"},
            {"启动器", "런처"}, {"调试选项", "디버그 옵션"}, {"文件下载源", "파일 다운로드 소스"}, {"版本列表源", "버전 목록 소스"},
            {"最大线程数", "최대 스레드 수"}, {"速度限制", "속도 제한"}, {"目标文件夹", "대상 폴더"}, {"游戏更新提示", "게임 업데이트 알림"},
            {"正式版更新提示", "정식 업데이트 알림"}, {"测试版更新提示", "스냅샷 업데이트 알림"}, {"游戏语言", "게임 언어"},
            {"自动设置为中文", "자동으로 중국어 설정"}, {"启动器更新", "런처 업데이트"}, {"启动器公告", "런처 공지"},
            {"缓存文件夹", "캐시 폴더"}, {"匿名数据收集", "익명 데이터 수집"}, {"检查更新", "업데이트 확인"},
            {"导出设置", "설정 내보내기"}, {"导入设置", "설정 가져오기"}, {"确定", "확인"}, {"取消", "취소"},
            {"警告", "경고"}, {"我知道了", "알겠습니다"}, {"正在刷新……", "새로 고치는 중..."}, {"开始", "시작"},
            {"关闭", "닫기"}, {"版本", "버전"}, {"整合包", "통합팩"}
        }},
        {"fr_FR", New Dictionary(Of String, String)() From {
            {"启动", "Lancer"}, {"下载", "Télécharger"}, {"联机", "Multijoueur"}, {"设置", "Paramètres"}, {"更多", "Plus"},
            {"全局设置", "Paramètres globaux"}, {"语言", "Langue"}, {"社区资源", "Ressources communautaires"}, {"辅助功能", "Accessibilité"},
            {"启动器", "Lanceur"}, {"调试选项", "Options de débogage"}, {"文件下载源", "Source de téléchargement"}, {"版本列表源", "Source de liste de versions"},
            {"最大线程数", "Threads max"}, {"速度限制", "Limite de vitesse"}, {"目标文件夹", "Dossier cible"}, {"游戏更新提示", "Notifications de mise à jour du jeu"},
            {"正式版更新提示", "Notifications de versions stables"}, {"测试版更新提示", "Notifications de versions de test"}, {"游戏语言", "Langue du jeu"},
            {"自动设置为中文", "Définir automatiquement en chinois"}, {"启动器更新", "Mises à jour du lanceur"}, {"启动器公告", "Annonces du lanceur"},
            {"缓存文件夹", "Dossier de cache"}, {"匿名数据收集", "Collecte de données anonymes"}, {"检查更新", "Vérifier les mises à jour"},
            {"导出设置", "Exporter les paramètres"}, {"导入设置", "Importer les paramètres"}, {"确定", "OK"}, {"取消", "Annuler"},
            {"警告", "Avertissement"}, {"我知道了", "Compris"}, {"正在刷新……", "Actualisation..."}, {"开始", "Démarrer"},
            {"关闭", "Fermer"}, {"版本", "Version"}, {"整合包", "Modpack"}
        }},
        {"de_DE", New Dictionary(Of String, String)() From {
            {"启动", "Start"}, {"下载", "Herunterladen"}, {"联机", "Online"}, {"设置", "Einstellungen"}, {"更多", "Mehr"},
            {"全局设置", "Globale Einstellungen"}, {"语言", "Sprache"}, {"社区资源", "Community-Ressourcen"}, {"辅助功能", "Bedienungshilfen"},
            {"启动器", "Launcher"}, {"调试选项", "Debug-Optionen"}, {"文件下载源", "Downloadquelle"}, {"版本列表源", "Versionslistenquelle"},
            {"最大线程数", "Maximale Threads"}, {"速度限制", "Geschwindigkeitslimit"}, {"目标文件夹", "Zielordner"}, {"游戏更新提示", "Spielupdate-Benachrichtigungen"},
            {"正式版更新提示", "Vollversions-Benachrichtigungen"}, {"测试版更新提示", "Snapshot-Benachrichtigungen"}, {"游戏语言", "Spielsprache"},
            {"自动设置为中文", "Automatisch auf Chinesisch setzen"}, {"启动器更新", "Launcher-Updates"}, {"启动器公告", "Launcher-Ankündigungen"},
            {"缓存文件夹", "Cache-Ordner"}, {"匿名数据收集", "Anonyme Datenerfassung"}, {"检查更新", "Nach Updates suchen"},
            {"导出设置", "Einstellungen exportieren"}, {"导入设置", "Einstellungen importieren"}, {"确定", "OK"}, {"取消", "Abbrechen"},
            {"警告", "Warnung"}, {"我知道了", "Verstanden"}, {"正在刷新……", "Aktualisiere..."}, {"开始", "Starten"},
            {"关闭", "Schließen"}, {"版本", "Version"}, {"整合包", "Modpack"}
        }},
        {"es_ES", New Dictionary(Of String, String)() From {
            {"启动", "Iniciar"}, {"下载", "Descargar"}, {"联机", "Multijugador"}, {"设置", "Ajustes"}, {"更多", "Más"},
            {"全局设置", "Ajustes globales"}, {"语言", "Idioma"}, {"社区资源", "Recursos de la comunidad"}, {"辅助功能", "Accesibilidad"},
            {"启动器", "Iniciador"}, {"调试选项", "Opciones de depuración"}, {"文件下载源", "Fuente de descarga"}, {"版本列表源", "Fuente de lista de versiones"},
            {"最大线程数", "Máximo de subprocesos"}, {"速度限制", "Límite de velocidad"}, {"目标文件夹", "Carpeta de destino"}, {"游戏更新提示", "Notificaciones de actualización del juego"},
            {"正式版更新提示", "Notificaciones de versiones estables"}, {"测试版更新提示", "Notificaciones de versiones de prueba"}, {"游戏语言", "Idioma del juego"},
            {"自动设置为中文", "Establecer automáticamente en chino"}, {"启动器更新", "Actualizaciones del iniciador"}, {"启动器公告", "Anuncios del iniciador"},
            {"缓存文件夹", "Carpeta de caché"}, {"匿名数据收集", "Recopilación de datos anónimos"}, {"检查更新", "Buscar actualizaciones"},
            {"导出设置", "Exportar ajustes"}, {"导入设置", "Importar ajustes"}, {"确定", "Aceptar"}, {"取消", "Cancelar"},
            {"警告", "Advertencia"}, {"我知道了", "Entendido"}, {"正在刷新……", "Actualizando..."}, {"开始", "Iniciar"},
            {"关闭", "Cerrar"}, {"版本", "Versión"}, {"整合包", "Modpack"}
        }},
        {"es_MX", New Dictionary(Of String, String)() From {
            {"启动", "Iniciar"}, {"下载", "Descargar"}, {"联机", "Multijugador"}, {"设置", "Ajustes"}, {"更多", "Más"}
        }},
        {"pt_PT", New Dictionary(Of String, String)() From {
            {"启动", "Iniciar"}, {"下载", "Descarregar"}, {"联机", "Multijogador"}, {"设置", "Definições"}, {"更多", "Mais"}
        }},
        {"pt_BR", New Dictionary(Of String, String)() From {
            {"启动", "Iniciar"}, {"下载", "Baixar"}, {"联机", "Multijogador"}, {"设置", "Configurações"}, {"更多", "Mais"},
            {"全局设置", "Configurações globais"}, {"语言", "Idioma"}, {"社区资源", "Recursos da comunidade"}, {"辅助功能", "Acessibilidade"},
            {"启动器", "Iniciador"}, {"调试选项", "Opções de depuração"}, {"文件下载源", "Origem de download"}, {"版本列表源", "Origem da lista de versões"},
            {"最大线程数", "Máximo de threads"}, {"速度限制", "Limite de velocidade"}, {"目标文件夹", "Pasta de destino"}, {"游戏更新提示", "Notificações de atualização do jogo"},
            {"正式版更新提示", "Notificações de versões estáveis"}, {"测试版更新提示", "Notificações de versões de teste"}, {"游戏语言", "Idioma do jogo"},
            {"自动设置为中文", "Definir automaticamente para chinês"}, {"启动器更新", "Atualizações do iniciador"}, {"启动器公告", "Anúncios do iniciador"},
            {"缓存文件夹", "Pasta de cache"}, {"匿名数据收集", "Coleta de dados anônimos"}, {"检查更新", "Verificar atualizações"},
            {"导出设置", "Exportar configurações"}, {"导入设置", "Importar configurações"}, {"确定", "OK"}, {"取消", "Cancelar"},
            {"警告", "Aviso"}, {"我知道了", "Entendi"}, {"正在刷新……", "Atualizando..."}, {"开始", "Iniciar"},
            {"关闭", "Fechar"}, {"版本", "Versão"}, {"整合包", "Modpack"}
        }},
        {"ru_RU", New Dictionary(Of String, String)() From {
            {"启动", "Запуск"}, {"下载", "Скачать"}, {"联机", "Онлайн"}, {"设置", "Настройки"}, {"更多", "Ещё"},
            {"全局设置", "Глобальные настройки"}, {"语言", "Язык"}, {"社区资源", "Ресурсы сообщества"}, {"辅助功能", "Специальные возможности"},
            {"启动器", "Запускатель"}, {"调试选项", "Параметры отладки"}, {"文件下载源", "Источник загрузки файлов"}, {"版本列表源", "Источник списка версий"},
            {"最大线程数", "Максимум потоков"}, {"速度限制", "Ограничение скорости"}, {"目标文件夹", "Целевая папка"}, {"游戏更新提示", "Уведомления об обновлении игры"},
            {"正式版更新提示", "Уведомления о стабильных версиях"}, {"测试版更新提示", "Уведомления о тестовых версиях"}, {"游戏语言", "Язык игры"},
            {"自动设置为中文", "Автоматически установить китайский"}, {"启动器更新", "Обновления запускателя"}, {"启动器公告", "Объявления запускателя"},
            {"缓存文件夹", "Папка кэша"}, {"匿名数据收集", "Анонимный сбор данных"}, {"检查更新", "Проверить обновления"},
            {"导出设置", "Экспорт настроек"}, {"导入设置", "Импорт настроек"}, {"确定", "ОК"}, {"取消", "Отмена"},
            {"警告", "Предупреждение"}, {"我知道了", "Понятно"}, {"正在刷新……", "Обновление..."}, {"开始", "Начать"},
            {"关闭", "Закрыть"}, {"版本", "Версия"}, {"整合包", "Модпак"}
        }},
        {"it_IT", New Dictionary(Of String, String)() From {
            {"启动", "Avvia"}, {"下载", "Scarica"}, {"联机", "Multigiocatore"}, {"设置", "Impostazioni"}, {"更多", "Altro"},
            {"全局设置", "Impostazioni globali"}, {"语言", "Lingua"}, {"社区资源", "Risorse della community"}, {"辅助功能", "Accessibilità"},
            {"启动器", "Avviatore"}, {"调试选项", "Opzioni di debug"}, {"文件下载源", "Origine download"}, {"版本列表源", "Origine elenco versioni"},
            {"最大线程数", "Thread massimi"}, {"速度限制", "Limite di velocità"}, {"目标文件夹", "Cartella di destinazione"}, {"游戏更新提示", "Notifiche aggiornamento gioco"},
            {"正式版更新提示", "Notifiche versioni stabili"}, {"测试版更新提示", "Notifiche versioni di test"}, {"游戏语言", "Lingua di gioco"},
            {"自动设置为中文", "Imposta automaticamente in cinese"}, {"启动器更新", "Aggiornamenti avviatore"}, {"启动器公告", "Annunci dell'avviatore"},
            {"缓存文件夹", "Cartella cache"}, {"匿名数据收集", "Raccolta dati anonimi"}, {"检查更新", "Controlla aggiornamenti"},
            {"导出设置", "Esporta impostazioni"}, {"导入设置", "Importa impostazioni"}, {"确定", "OK"}, {"取消", "Annulla"},
            {"警告", "Avviso"}, {"我知道了", "Ho capito"}, {"正在刷新……", "Aggiornamento..."}, {"开始", "Avvia"},
            {"关闭", "Chiudi"}, {"版本", "Versione"}, {"整合包", "Modpack"}
        }},
        {"nl_NL", New Dictionary(Of String, String)() From {
            {"启动", "Starten"}, {"下载", "Downloaden"}, {"联机", "Online"}, {"设置", "Instellingen"}, {"更多", "Meer"}
        }},
        {"pl_PL", New Dictionary(Of String, String)() From {
            {"启动", "Uruchom"}, {"下载", "Pobierz"}, {"联机", "Online"}, {"设置", "Ustawienia"}, {"更多", "Więcej"}
        }},
        {"tr_TR", New Dictionary(Of String, String)() From {
            {"启动", "Başlat"}, {"下载", "İndir"}, {"联机", "Çevrimiçi"}, {"设置", "Ayarlar"}, {"更多", "Daha fazla"}
        }},
        {"uk_UA", New Dictionary(Of String, String)() From {
            {"启动", "Запуск"}, {"下载", "Завантажити"}, {"联机", "Онлайн"}, {"设置", "Налаштування"}, {"更多", "Більше"}
        }},
        {"ar_SA", New Dictionary(Of String, String)() From {
            {"启动", "تشغيل"}, {"下载", "تنزيل"}, {"联机", "عبر الإنترنت"}, {"设置", "الإعدادات"}, {"更多", "المزيد"}
        }},
        {"ar_EG", New Dictionary(Of String, String)() From {
            {"启动", "تشغيل"}, {"下载", "تنزيل"}, {"联机", "عبر الإنترنت"}, {"设置", "الإعدادات"}, {"更多", "المزيد"}
        }},
        {"he_IL", New Dictionary(Of String, String)() From {
            {"启动", "הפעלה"}, {"下载", "הורדה"}, {"联机", "מרובה משתתפים"}, {"设置", "הגדרות"}, {"更多", "עוד"}
        }},
        {"th_TH", New Dictionary(Of String, String)() From {
            {"启动", "เปิด"}, {"下载", "ดาวน์โหลด"}, {"联机", "ออนไลน์"}, {"设置", "การตั้งค่า"}, {"更多", "เพิ่มเติม"}
        }},
        {"vi_VN", New Dictionary(Of String, String)() From {
            {"启动", "Khởi động"}, {"下载", "Tải xuống"}, {"联机", "Trực tuyến"}, {"设置", "Cài đặt"}, {"更多", "Khác"}
        }},
        {"id_ID", New Dictionary(Of String, String)() From {
            {"启动", "Mulai"}, {"下载", "Unduh"}, {"联机", "Online"}, {"设置", "Pengaturan"}, {"更多", "Lainnya"}
        }},
        {"ms_MY", New Dictionary(Of String, String)() From {
            {"启动", "Mula"}, {"下载", "Muat turun"}, {"联机", "Dalam talian"}, {"设置", "Tetapan"}, {"更多", "Lagi"}
        }},
        {"fil_PH", New Dictionary(Of String, String)() From {
            {"启动", "Simulan"}, {"下载", "I-download"}, {"联机", "Online"}, {"设置", "Mga Setting"}, {"更多", "Higit pa"}
        }},
        {"hi_IN", New Dictionary(Of String, String)() From {
            {"启动", "लॉन्च"}, {"下载", "डाउनलोड"}, {"联机", "ऑनलाइन"}, {"设置", "सेटिंग्स"}, {"更多", "अधिक"}
        }},
        {"bn_BD", New Dictionary(Of String, String)() From {
            {"启动", "চালু"}, {"下载", "ডাউনলোড"}, {"联机", "অনলাইন"}, {"设置", "সেটিংস"}, {"更多", "আরও"}
        }},
        {"ta_IN", New Dictionary(Of String, String)() From {
            {"启动", "தொடங்கு"}, {"下载", "பதிவிறக்கம்"}, {"联机", "ஆன்லைன்"}, {"设置", "அமைப்புகள்"}, {"更多", "மேலும்"}
        }},
        {"te_IN", New Dictionary(Of String, String)() From {
            {"启动", "ప్రారంభించు"}, {"下载", "డౌన్లోడ్"}, {"联机", "ఆన్లైన్"}, {"设置", "సెట్టింగ్లు"}, {"更多", "మరిన్ని"}
        }},
        {"mr_IN", New Dictionary(Of String, String)() From {
            {"启动", "लाँच"}, {"下载", "डाउनलोड"}, {"联机", "ऑनलाइन"}, {"设置", "सेटिंग्ज"}, {"更多", "अधिक"}
        }},
        {"gu_IN", New Dictionary(Of String, String)() From {
            {"启动", "લોંચ"}, {"下载", "ડાઉનલોડ"}, {"联机", "ઑનલાઇન"}, {"设置", "સેટિંગ્સ"}, {"更多", "વધુ"}
        }},
        {"kn_IN", New Dictionary(Of String, String)() From {
            {"启动", "ಪ್ರಾರಂಭಿಸು"}, {"下载", "ಡೌನ್ಲೋಡ್"}, {"联机", "ಆನ್ಲೈನ್"}, {"设置", "ಸೆಟ್ಟಿಂಗ್ಗಳು"}, {"更多", "ಹೆಚ್ಚು"}
        }},
        {"ml_IN", New Dictionary(Of String, String)() From {
            {"启动", "ആരംഭിക്കുക"}, {"下载", "ഡൗൺലോഡ്"}, {"联机", "ഓൺലൈൻ"}, {"设置", "ക്രമീകരണങ്ങൾ"}, {"更多", "കൂടുതൽ"}
        }},
        {"pa_IN", New Dictionary(Of String, String)() From {
            {"启动", "ਲਾਂਚ"}, {"下载", "ਡਾਊਨਲੋਡ"}, {"联机", "ਆਨਲਾਈਨ"}, {"设置", "ਸੈਟਿੰਗਾਂ"}, {"更多", "ਹੋਰ"}
        }},
        {"ne_NP", New Dictionary(Of String, String)() From {
            {"启动", "सुरु"}, {"下载", "डाउनलोड"}, {"联机", "अनलाइन"}, {"设置", "सेटिङ"}, {"更多", "थप"}
        }},
        {"si_LK", New Dictionary(Of String, String)() From {
            {"启动", "අරඹන්න"}, {"下载", "බාගත"}, {"联机", "සබැඳි"}, {"设置", "සැකසුම්"}, {"更多", "තවත්"}
        }},
        {"ur_PK", New Dictionary(Of String, String)() From {
            {"启动", "لانچ"}, {"下载", "ڈاؤن لوڈ"}, {"联机", "آن لائن"}, {"设置", "ترتیبات"}, {"更多", "مزید"}
        }},
        {"fa_IR", New Dictionary(Of String, String)() From {
            {"启动", "راه‌اندازی"}, {"下载", "دانلود"}, {"联机", "آنلاین"}, {"设置", "تنظیمات"}, {"更多", "بیشتر"}
        }},
        {"cs_CZ", New Dictionary(Of String, String)() From {
            {"启动", "Spustit"}, {"下载", "Stáhnout"}, {"联机", "Online"}, {"设置", "Nastavení"}, {"更多", "Více"}
        }},
        {"sk_SK", New Dictionary(Of String, String)() From {
            {"启动", "Spustiť"}, {"下载", "Stiahnuť"}, {"联机", "Online"}, {"设置", "Nastavenia"}, {"更多", "Viac"}
        }},
        {"hu_HU", New Dictionary(Of String, String)() From {
            {"启动", "Indítás"}, {"下载", "Letöltés"}, {"联机", "Online"}, {"设置", "Beállítások"}, {"更多", "Több"}
        }},
        {"ro_RO", New Dictionary(Of String, String)() From {
            {"启动", "Pornire"}, {"下载", "Descărcare"}, {"联机", "Online"}, {"设置", "Setări"}, {"更多", "Mai mult"}
        }},
        {"bg_BG", New Dictionary(Of String, String)() From {
            {"启动", "Старт"}, {"下载", "Изтегляне"}, {"联机", "Онлайн"}, {"设置", "Настройки"}, {"更多", "Още"}
        }},
        {"hr_HR", New Dictionary(Of String, String)() From {
            {"启动", "Pokreni"}, {"下载", "Preuzmi"}, {"联机", "Online"}, {"设置", "Postavke"}, {"更多", "Više"}
        }},
        {"sr_RS", New Dictionary(Of String, String)() From {
            {"启动", "Покрени"}, {"下载", "Преузимање"}, {"联机", "Онлајн"}, {"设置", "Подешавања"}, {"更多", "Више"}
        }},
        {"sl_SI", New Dictionary(Of String, String)() From {
            {"启动", "Zaženi"}, {"下载", "Prenesi"}, {"联机", "Splet"}, {"设置", "Nastavitve"}, {"更多", "Več"}
        }},
        {"lt_LT", New Dictionary(Of String, String)() From {
            {"启动", "Paleisti"}, {"下载", "Atsisiųsti"}, {"联机", "Internetas"}, {"设置", "Nustatymai"}, {"更多", "Daugiau"}
        }},
        {"lv_LV", New Dictionary(Of String, String)() From {
            {"启动", "Palaišana"}, {"下载", "Lejupielādēt"}, {"联机", "Tiešsaiste"}, {"设置", "Iestatījumi"}, {"更多", "Vairāk"}
        }},
        {"et_EE", New Dictionary(Of String, String)() From {
            {"启动", "Käivita"}, {"下载", "Allalaadimine"}, {"联机", "Võrgus"}, {"设置", "Seaded"}, {"更多", "Rohkem"}
        }},
        {"el_GR", New Dictionary(Of String, String)() From {
            {"启动", "Εκκίνηση"}, {"下载", "Λήψη"}, {"联机", "Διαδίκτυο"}, {"设置", "Ρυθμίσεις"}, {"更多", "Περισσότερα"}
        }},
        {"da_DK", New Dictionary(Of String, String)() From {
            {"启动", "Start"}, {"下载", "Hent"}, {"联机", "Online"}, {"设置", "Indstillinger"}, {"更多", "Mere"}
        }},
        {"sv_SE", New Dictionary(Of String, String)() From {
            {"启动", "Starta"}, {"下载", "Ladda ner"}, {"联机", "Online"}, {"设置", "Inställningar"}, {"更多", "Mer"}
        }},
        {"nb_NO", New Dictionary(Of String, String)() From {
            {"启动", "Start"}, {"下载", "Last ned"}, {"联机", "Online"}, {"设置", "Innstillinger"}, {"更多", "Mer"}
        }},
        {"fi_FI", New Dictionary(Of String, String)() From {
            {"启动", "Käynnistä"}, {"下载", "Lataa"}, {"联机", "Verkko"}, {"设置", "Asetukset"}, {"更多", "Lisää"}
        }},
        {"is_IS", New Dictionary(Of String, String)() From {
            {"启动", "Ræsa"}, {"下载", "Sækja"}, {"联机", "Net"}, {"设置", "Stillingar"}, {"更多", "Meira"}
        }},
        {"ga_IE", New Dictionary(Of String, String)() From {
            {"启动", "Seoladh"}, {"下载", "Íoslódáil"}, {"联机", "Ar Líne"}, {"设置", "Socruithe"}, {"更多", "Tuilleadh"}
        }},
        {"cy_GB", New Dictionary(Of String, String)() From {
            {"启动", "Cychwyn"}, {"下载", "Lawrlwytho"}, {"联机", "Ar-lein"}, {"设置", "Gosodiadau"}, {"更多", "Mwy"}
        }},
        {"eu_ES", New Dictionary(Of String, String)() From {
            {"启动", "Abiarazi"}, {"下载", "Deskargatu"}, {"联机", "Linean"}, {"设置", "Ezarpenak"}, {"更多", "Gehiago"}
        }},
        {"ca_ES", New Dictionary(Of String, String)() From {
            {"启动", "Inicia"}, {"下载", "Baixa"}, {"联机", "En línia"}, {"设置", "Configuració"}, {"更多", "Més"}
        }},
        {"gl_ES", New Dictionary(Of String, String)() From {
            {"启动", "Iniciar"}, {"下载", "Descargar"}, {"联机", "En liña"}, {"设置", "Axustes"}, {"更多", "Máis"}
        }},
        {"az_AZ", New Dictionary(Of String, String)() From {
            {"启动", "Başlat"}, {"下载", "Endir"}, {"联机", "Onlayn"}, {"设置", "Parametrlər"}, {"更多", "Daha çox"}
        }},
        {"kk_KZ", New Dictionary(Of String, String)() From {
            {"启动", "Іске қосу"}, {"下载", "Жүктеу"}, {"联机", "Онлайн"}, {"设置", "Баптаулар"}, {"更多", "Тағы"}
        }},
        {"uz_UZ", New Dictionary(Of String, String)() From {
            {"启动", "Ishga tushirish"}, {"下载", "Yuklab olish"}, {"联机", "Onlayn"}, {"设置", "Sozlamalar"}, {"更多", "Ko'proq"}
        }},
        {"hy_AM", New Dictionary(Of String, String)() From {
            {"启动", "Գործարկել"}, {"下载", "Ներբեռնել"}, {"联机", "Առցանց"}, {"设置", "Կարգավորումներ"}, {"更多", "Ավելին"}
        }},
        {"ka_GE", New Dictionary(Of String, String)() From {
            {"启动", "გაშვება"}, {"下载", "ჩამოტვირთვა"}, {"联机", "ონლაინ"}, {"设置", "პარამეტრები"}, {"更多", "მეტი"}
        }},
        {"sw_KE", New Dictionary(Of String, String)() From {
            {"启动", "Anzisha"}, {"下载", "Pakua"}, {"联机", "Mtandaoni"}, {"设置", "Mipangilio"}, {"更多", "Zaidi"}
        }},
        {"af_ZA", New Dictionary(Of String, String)() From {
            {"启动", "Begin"}, {"下载", "Aflaai"}, {"联机", "Aanlyn"}, {"设置", "Instellings"}, {"更多", "Meer"}
        }},
        {"sq_AL", New Dictionary(Of String, String)() From {
            {"启动", "Nis"}, {"下载", "Shkarko"}, {"联机", "Online"}, {"设置", "Cilësimet"}, {"更多", "Më shumë"}
        }},
        {"mk_MK", New Dictionary(Of String, String)() From {
            {"启动", "Старт"}, {"下载", "Преземи"}, {"联机", "Онлајн"}, {"设置", "Поставки"}, {"更多", "Повеќе"}
        }}
    }

End Module
