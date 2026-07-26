# PTFSkillMod - Pass The Fear Mod

> BepInEx IL2CPP mod for the roguelike game **Pass The Fear**.

---

## 中文

### 功能

**1. 专精3选2**
原版每个专精层只能选1个技能，改为可保留 **2个**。
数据自动持久化到文件，重进游戏不丢失。

**2. Boss血量递增**
每击败一个Boss，后续Boss血量 = 原始HP × (1 + 击败数×2)。
示例：击败3个后 → 下一个Boss为 **7倍** 血量。
击杀数自动保存，重进游戏不丢失。

**3. 自动秘宝合成**
背包中有同种秘宝时，自动免费合成为高级版本。

**4. Boss掉落药水**
每击败一个Boss，在地图上生成"传奇铁锤"药水。

### 安装
1. 安装 BepInEx 6.0.0 IL2CPP 到游戏目录，版本：BeplnEx-Unity.IL2CPP-win-x64-6.0.0。地址：https://builds.bepinex.dev/projects/bepinex_be
2. 将BeplnEx文件解压至游戏根目录下，首先运行一次游戏生成所需文件，进入游戏直接退出。
3. 将 `PTFSkillMod.dll` 放入 `BepInEx/plugins/`
4. 启动游戏

### 文件
```
BepInEx/
├── plugins/
│   └── PTFSkillMod.dll
└── config/
    └── com.violet.mod.skilltree3to2/
        ├── skilldata.txt      # 专精数据
        └── bosscount.txt      # Boss击杀数
```

### 日志
运行日志位于 `BepInEx/LogOutput.log`。

---

## English

### Features

**1. Skill Tree 3→2**
Pick **2 skills** per layer instead of 1. Selections persist across restarts.

**2. Boss HP Scaling**
Each kill multiplies subsequent boss HP by `1 + defeated × 2`.
Example: 3 kills → next boss has **7×** HP.
Kill count persists across restarts.

**3. Auto Relic Combine**
2+ identical relics in backpack → auto-combine into forged version for free.

**4. Boss Potion Drop**
Each boss kill spawns a "Legendary Hammer" potion on the map.

### Install
1. Install BepInEx 6.0.0 IL2CPP to game directory
2. Place `PTFSkillMod.dll` into `BepInEx/plugins/`
3. Launch the game

### Files
```
BepInEx/
├── plugins/
│   └── PTFSkillMod.dll
└── config/
    └── com.violet.mod.skilltree3to2/
        ├── skilldata.txt      # skill data
        └── bosscount.txt      # boss kill count
```

### Logs
Check `BepInEx/LogOutput.log` for runtime logs.

---

## Credits | 感谢

[BepInEx](https://github.com/BepInEx/BepInEx) · [Harmony](https://github.com/pardeike/Harmony) · [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
