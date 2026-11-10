require "FairyGUI"

local CounterPanel = fgui.extension_class(GComponent)
local panel
local ownsPackage = false
ToLuaCounter = {}

-- 组件已构建完毕；取控件、绑定事件和计数状态全部在 Lua。
function CounterPanel:ctor()
    self.addButton = self:GetChild("b1")
    self.resetButton = self:GetChild("b0")
    self.addButton.icon = "Cooldown/k1"
    self.resetButton.icon = "Cooldown/k0"
    self.addButton:GetChild("mask").visible = false
    self.resetButton:GetChild("mask").visible = false
    self.count = 0
    -- 每个实例保存自己的回调，重开时不复用已释放的 peer table。
    self.onAdd = function() self:OnAdd() end
    self.onReset = function() self:OnReset() end
    self.addButton.onClick:Add(self.onAdd)
    self.resetButton.onClick:Add(self.onReset)
    self:Refresh()
end

function CounterPanel:OnAdd()
    self.count = self.count + 1
    self:Refresh()
end

function CounterPanel:OnReset()
    self.count = 0
    self:Refresh()
end

function CounterPanel:Refresh()
    self.addButton.text = tostring(self.count)
end

function ToLuaCounter.Start()
    if panel then return end
    ownsPackage = UIPackage.GetByName("Cooldown") == nil
    UIPackage.AddPackage("UI/Cooldown")
    -- 注册必须早于 CreateObject；使用 FairyGUI 官方 ToLua 扩展。
    fgui.register_extension("ui://Cooldown/Main", CounterPanel)
    panel = UIPackage.CreateObject("Cooldown", "Main")
    GRoot.inst:AddChild(panel)
    panel:Center()
end

function ToLuaCounter.Stop()
    if not panel then return end
    panel.addButton.onClick:Remove(panel.onAdd)
    panel.resetButton.onClick:Remove(panel.onReset)
    panel:Dispose()
    panel = nil
    -- 清掉工厂对 Lua 回调的引用，再允许宿主关闭 LuaState。
    FairyGUI.UIObjectFactory.SetPackageItemExtension("ui://Cooldown/Main", typeof(GComponent))
    if ownsPackage then UIPackage.RemovePackage("Cooldown") end
    ownsPackage = false
end

function ToLuaCounter.Check()
    assert(panel and panel.count == tonumber(panel.addButton.text), "Lua extension not initialized")
    panel.resetButton.onClick:Call()
    panel.addButton.onClick:Call()
    panel.addButton.onClick:Call()
    assert(panel.count == 2 and panel.addButton.text == "2", "Click must increment exactly once")
    panel.resetButton.onClick:Call()
    assert(panel.count == 0 and panel.addButton.text == "0", "Reset failed")
    local previous = panel
    ToLuaCounter.Stop()
    assert(previous.isDisposed, "Old view was not disposed")
    ToLuaCounter.Start()
    panel.addButton.onClick:Call()
    assert(panel.count == 1 and panel.addButton.text == "1", "Reopen duplicated listeners")
    panel.resetButton.onClick:Call()
    print("[ToLua TEST PASS] extension, click, reset, dispose, reopen")
end

