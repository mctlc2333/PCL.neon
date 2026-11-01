# Plain Craft Launcher neon（社区分支版）

[![Stars](https://img.shields.io/github/stars/mctlc2333/PCL.neon?style=flat&label=Stars&labelColor=444444&color=eac54f)](https://github.com/mctlc2333/PCL.neon)
[![Issues](https://img.shields.io/github/issues/mctlc2333/PCL.neon?style=flat&label=Issues&labelColor=444444&color=1F883D)](https://github.com/mctlc2333/PCL.neon/issues)

基于官方开源仓库 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)
（上游 HEAD `93ede5d`，2.99.0.1 正式版）的**社区分支版本**，由社区维护者
**mcltc2333** 维护，在原版基础上做了面向国内玩家与插件生态的功能增强，
持续跟进上游更新。

代码在每次 PCL 发布更新时同步一次上游，并叠加本分支的社区改动。

<br/>

### 本分支主要改动（相对上游 `93ede5d`）

1. **修复 Minecraft 游戏本体下载**（官方 2.99.0.0 起因合规移除，本分支恢复全部入口）
2. **修复整合包下载 / 安装**（CurseForge + Modrinth，内置组织 API Key）
3. **增加更换语言功能，支持 73 种语言**（切换即时生效）
4. **新增插件系统**（`plugins\*.dll` 鸭子类型加载 + 宿主 API）
5. **品牌更名为 Plain Craft Launcher neon**（名称 / 作者 / 版本号 1.10）
6. **复活联机功能并改用陶瓦联机（Terracotta）**（房间码联机，局域网互通）
7. **新增网易中国版（X19）与 4399 登录**（参照 FantNel 脱盒方案，阶段 1：登录换凭证）
8. **恢复联机设置入口，启用并新增页面隐藏开关**（设置 → 个性化 → 功能隐藏，可隐藏顶部导航「联机」「插件」等入口；任意界面按 F12 临时显示）
9. **新增网易中国版下载**（下载页「中国版」入口：选择网络服 / 租赁服后按 FantNel 流程自动下载网易中国版客户端——基础包 → 版本包 → 核心库 → 白端基础模组；基础模组下载需使用已登录的网易（X19）/ 4399 账号签名）

### 使用方式

- 直接下载交付目录 `E:\neon\PCL-fork-delivery\` 中的 `Plain Craft Launcher 2.exe`
  或通过补丁自行构建（详见交付目录 `README.md`）；
- 开发插件：参照 `pcl-plugin-example\` 示例工程，编译后放入启动器同目录 `plugins\` 即可。

### 相关资源

- 官方上游仓库：https://github.com/Meloong-Git/PCL
- 官方 PCL 下载：https://meloong.com/pcl
- 帮助文档库：https://github.com/LTCatt/PCL2Help
- 特别鸣谢：https://github.com/NirvanaTec/Fantnel
- 本分支源码：`main`（本地仓库 `E:\neon\PCL`，已同步至本仓库）
