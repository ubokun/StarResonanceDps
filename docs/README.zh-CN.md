# 别查我DPS

[日本語](https://github.com/kokoukohiro/StarResonanceDps) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [English](README.en.md)

显示《星痕共鸣》战斗信息(DPS等)的非官方工具(Windows版)。

![DPS计量器](wiki/images/zh-CN/DpsMeter1.png)

实时显示战斗状况，也可以回顾以往战斗的结果。还支持战斗中事件的通知和功能扩展。

不修改游戏客户端，也不会向外部发送战斗数据。请勿将显示的数值用于诽谤中伤其他玩家等破坏社区环境的目的。

## 所需环境

- Windows(64位)
- [.NET 10桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Npcap](https://npcap.com/)

## 安装方法

1. 从[发行版](https://github.com/kokoukohiro/StarResonanceDps/releases)获取`StarResonanceDps-<版本>.zip`
2. 将zip解压到可写入的文件夹
3. 启动文件夹中的`StarResonanceDps.exe`

- 设置和战斗历史记录会保存在此文件夹中
- 首次启动时，可能会出现Windows的警告(SmartScreen)。点击“更多信息”后即可选择“仍要运行”
- 启动用于快捷键的辅助程序时，会出现用户帐户控制的确认提示。如不使用快捷键，请在设置的启动设置中关闭“应用启动时运行快捷键辅助程序”
- 启动后要进行的操作，请参阅[初始设置](https://github.com/kokoukohiro/StarResonanceDps/wiki/初始设置)

## 更新

可以在应用内更新到新版本。有新版本时，启动时会出现确认窗口。也可以点击“帮助”中的“检查更新”进行确认。详情请参阅[安装与更新](https://github.com/kokoukohiro/StarResonanceDps/wiki/安装与更新)。

## 使用方法

界面和设置的说明请参阅[Wiki](https://github.com/kokoukohiro/StarResonanceDps/wiki/别查我DPS-Wiki)。

## 插件

可以通过插件添加功能。插件在应用中运行，可以进行与应用相同的操作。请只安装来自可信发布来源的插件。

开发方法请参阅[插件开发](https://github.com/kokoukohiro/StarResonanceDps/wiki/插件开发)。SDK包(`.nupkg`)与应用的zip一起放在发行版中。

## 问题反馈与建议

可以通过应用中的“反馈”向GitHub的Issue提交。提交需要GitHub账号。

## 通信

除已安装的插件外，应用仅在以下情况下连接外部。不会发送战斗数据。

- 检查更新和下载(GitHub)
- 在“反馈”中确认是否已反馈时(GitHub)
- 在“玩家信息”中显示头像和名片照片时(游戏发来的照片URL)

## 许可证

- 本体采用GNU GPL v3(含附加许可)，SDK、分析器、示例和文档采用MIT。详情请参阅[LICENSING.md](../LICENSING.md)
- 所用库的许可证请参阅[THIRD-PARTY-NOTICES.txt](../THIRD-PARTY-NOTICES.txt)
- 本工具为非官方工具，与游戏的开发和运营方无关。游戏名称、数据和图片的权利归各自的权利人所有
