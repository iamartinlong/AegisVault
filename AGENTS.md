# AGENTS.md —— AegisVault 开发约定

> 面向在本仓库工作的开发者与 AI 代理：**只放"每次都会用到"的硬规则**。
> 细节知识库（本地 `docs/`，不随仓库分发）：`开发最佳实践.md`（规则）、`踩坑记录.md`（P-01…P-35 事故复盘）、`数据迁移设计.md`（持久化硬约束）、`交接文档.md`（状态/待办/已知问题）。

## 项目速览
- .NET 10 + Avalonia 12.1 + AtomUI 6.1 的本地优先零知识密码管理器；GPL-3.0
- 分层：`AegisVault.Core`（纯逻辑、`IsAotCompatible`）/ `AegisVault.Platform`（OS 能力）/ `AegisVault.App`（Avalonia UI）/ `tests/*`（Core/Platform/App 三个工程）
- 存储：加密 SQLite（`.aegis`）；条目与设置负载均带版本且绑定 AAD（`id/用途 + version`）
- NativeAOT 单文件发布；禁止反射序列化（一律 `VaultJsonContext` 源生成）

## 必跑命令（门禁）
```powershell
Stop-Process -Name AegisVault.App -Force            # 构建/测试前必关，否则 bin 被锁（MSB3026/3027）
dotnet build -c Release -warnaserror --no-restore   # 期望：0 警告
dotnet test  -c Release --no-build                  # 期望：全部通过
```
- 依赖变更或首次构建：先 `dotnet restore`，再 `dotnet build --no-restore`（直连网络可能卡住）
- 冷启动 10–12 秒属正常（AtomUI 主题初始化 ~6 秒），不是死锁

## 硬性约束（违反必踩坑）
1. **新增落盘字段 = 四件套**：负载版本 +1、迁移分支（`ModelMigrations`）、加载归一化、契约测试。详见 `docs/数据迁移设计.md`。
   - **源码生成 JSON 对缺失集合成员返回 `null`**（反射路径才执行属性初始化器）→ 集合字段必须"加载期归一化 + 消费端空安全"。
   - 版本必须参与 AAD；旧负载可升级、新版本负载必须明确报错，不得静默误读。
2. **AtomUI 图标不能靠样式/继承上色**（基类与精确类型选择器均不生效、宿主 Foreground 被图标主题覆盖）→ 用 `atom:Button` + `Icon` + `Foreground`；纯展示用不可交互小按钮。
3. **浮层滚动条会盖住内容**：贴右边缘的控件/按钮预留 ≥16px（如 `Margin="0,0,16,x"`）；验收时悬停滚动条展开态检查。
4. **`docs/` 中文文档禁止用 PowerShell 文本管道改写**（`Get-Content`/`Set-Content` 按 ANSI 读写会丢字吞行）；用编辑器或专用写文件工具，批量替换用带上下文的精确编辑。
5. **`git push` 卡死**用 `git -c http.version=HTTP/1.1 push origin master`；卡死后先清理残留 `git` 进程。
6. **危险操作必须二次确认**（删除条目/分类等）；表单校验**一次性提示全部错误**，用户修改字段即清除对应错误。
7. **文案改动同步 zh/en 两张表**（单测会拦）；语言在启动时解析一次，切换需重启。
8. **禁止提交**：`docs/`（本地文档）、`*.aegis`（库文件）、`app.json`（个人偏好）、任何临时调试钩子（如截图用的环境变量开关）。
9. **颜色只用 Token**：AtomUI `{atom:SharedTokenResource ...}`、应用自有 `{DynamicResource AegisXxxBrush}`（浅/深两套）；禁止硬编码品牌色。
10. **AtomUI 以源码为准，不信官网文档**：文档版本落后于锁定的 6.1.9，控件 API/行为常对不上（实例：`LineEdit.InnerLeftContent` 实为继承 Avalonia `TextBox`；`DropdownButton` 隐藏箭头要 `IsShowOpenIndicator=False`（`IsArrowVisible` 无效）；`TriggerType=Click` 只认 `PointerPressed`）。查证顺序：**① GitHub 源码（对应 tag）→ ② 反射探针 `%TEMP%\opencode\atomui-probe` → ③ 官网文档（仅参考）**。
    - 取源码（`raw.githubusercontent.com` 本机常不可达，走 API）：先 `https://api.github.com/repos/AtomUI/AtomUI/tags` 拿 tag（如 `v6.1.9`）与 sha，用 `git/trees/<sha>?recursive=1` 搜文件路径，再 `curl -H "Accept: application/vnd.github.raw" "https://api.github.com/repos/AtomUI/AtomUI/contents/<path>?ref=v6.1.9"` 取文件；主题/行为多在 `src/AtomUI.Desktop.Controls/<控件>/` 与 `controlgallery/`（官方用法示例）。

## 代码约定
- UI 事件处理走 code-behind 或命令，保持 XAML **编译绑定**（`x:DataType`）。
- 窗口统一用 `Controls/AegisTitleBar`（扩展客户区 + 自绘最小/最大/关闭；处理拖拽、双击最大化、按钮命中不拖动）。
- `IValueConverter` 返回控件时**每次新建实例**（只缓存创建函数，禁止缓存控件）。
- 时间与外部副作用一律注入：`TimeProvider`、`IUrlLauncher`、`IClipboardAccess`、`IKeyProtector`。
- 服务层保持语言中立（Core 返回枚举，App 负责本地化文案）。

## 验证方式
| 改动类型 | 验证 |
|---|---|
| 任意代码 | 构建 0 警告 + 全量测试 |
| UI | 追加截图：浅色中 + 深色英（脚本 `%TEMP%\opencode\capture-*.ps1`，自动备份/还原 `app.json`） |
| 数据/迁移 | 旧格式负载往返测试 + 版本绑定/守卫测试（见 `docs/数据迁移设计.md`） |
| 发布相关 | `dotnet publish -c Release -r win-x64 -p:PublishAot=true` 冒烟（先关运行实例） |

## 开发工具（本机，临时目录）
- 演示库：`dotnet run --project %TEMP%\opencode\aegis-seed -- <路径>\vault.aegis zh`（含设备密钥/分类，`DisableScreenCapture=false`，否则截图全黑）
- 截图脚本：`%TEMP%\opencode\capture-*.ps1`（UIA 驱动；点击自动化前须先置顶窗口；**下拉菜单/浮层**用 `AutomationId` 定位 + `SetCursorPos`/`mouse_event` 物理点击，浮层打开后勿再改前台窗口，详见 `docs/踩坑记录.md` P-34）
- 反射探针：`%TEMP%\opencode\atomui-probe`（加载目标程序集输出类型/基类/属性/方法；查 API 与源码并列第一优先）

## 提交习惯
- 小步提交；信息 `feat|fix|style|docs|chore(范围): 描述`
- 提交前 `git status` 复核（无 docs/、库文件、偏好文件、调试钩子混入）
- 已知问题与待办以 `docs/交接文档.md` 为准（公开仓库里没有这些文档）
