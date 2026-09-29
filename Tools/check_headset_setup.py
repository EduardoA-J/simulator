"""Read-only asset checks; this does not replace Unity compilation or headset testing."""
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def blocks(path):
    return {b.splitlines()[0]: b for b in read(path).split("--- !u!114 &")[1:]}


checks = []


def check(condition, message):
    if not condition:
        raise AssertionError(message)
    checks.append(message)


xr = blocks("Assets/XR/XRGeneralSettingsPerBuildTarget.asset")
loader_guid = re.search(r"guid: (\w+)", read("Assets/XR/Loaders/OpenXRLoader.asset.meta"))[1]
openxr = blocks("Assets/XR/Settings/OpenXRPackageSettings.asset")
for platform in ("Android", "Standalone"):
    general = next(b for b in xr.values() if f"  m_Name: {platform} Settings\n" in b)
    check("m_InitManagerOnStart: 1" in general, platform + ": XR starts automatically")
    manager_id = re.search(r"m_LoaderManagerInstance: \{fileID: (-?\d+)", general)[1]
    check(loader_guid in xr[manager_id], platform + ": OpenXR loader assigned")
    if platform == "Standalone":
        check("264fc24f5797ff047bba4514d92ed96d" not in xr[manager_id], "Standalone: no SimulationLoader")
    settings = next(b for b in openxr.values() if f"  m_Name: {platform}\n" in b)
    feature_ids = re.findall(r"  - \{fileID: (-?\d+)\}", settings)
    features = [openxr[i] for i in feature_ids]
    check(any("OculusTouchControllerProfile" in f and "m_enabled: 1" in f for f in features),
          platform + ": Touch controller profile enabled and referenced")
    if platform == "Android":
        check(any("m_Name: MetaQuestFeature Android" in f and "m_enabled: 1" in f for f in features),
              "Android: Meta Quest support enabled and referenced")

player = read("ProjectSettings/ProjectSettings.asset")
check("AndroidTargetArchitectures: 2" in player, "Android: ARM64")
check(re.search(r"scriptingBackend:\s+Android: 1", player), "Android: IL2CPP")
check("activeInputHandler: 1" in player, "Input System enabled")
check("companyName: David" in player and "productName: Aviones de Papel VR" in player, "App identity: David / Aviones de Papel VR")
check("Android: com.david.avionespapel" in player, "Android application identifier")
check("AndroidMinSdkVersion: 32" in player and "AndroidTargetSdkVersion: 34" in player, "Android SDK: minimum 32, target 34")
icon_path = "Assets/AvionesPapelVR/07_Textures/AppIcon.png"
icon = (ROOT / icon_path).read_bytes()
check(icon[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", icon[16:24]) == (512, 512), "App icon: 512x512 PNG")
icon_guid = re.search(r"guid: (\w+)", read(icon_path + ".meta"))[1]
default_icons = player.split("m_BuildTargetIcons:", 1)[1].split("m_BuildTargetPlatformIcons:", 1)[0]
android_icons = player.split("m_BuildTargetPlatformIcons:", 1)[1].split("m_BuildTarget: Android", 1)[1].split("m_BuildTarget:", 1)[0]
check(icon_guid in default_icons and icon_guid in android_icons, "App icon assigned to default and Android slots")
quest = next(b for b in openxr.values() if "m_Name: MetaQuestFeature Android" in b)
devices = dict(re.findall(r"manifestName: (\w+)\s+enabled: (\d+)", quest))
check(devices.get("quest", "0") == "0" and all(devices.get(name) == "1" for name in ("quest2", "cambria", "eureka", "quest3s")),
      "Quest 2, Pro, 3 and 3S enabled; Quest 1 excluded")
check("m_AutomaticallyInstantiateSimulatorPrefab: 0" in read(
    "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset"), "No automatic simulated controllers")
scene = read("Assets/AvionesPapelVR/00_Scenes/Game_VR_Oculus.unity")
gm = next(b for b in scene.split("---") if "Assembly-CSharp::AvionesPapelVR.GameManager" in b)
check("vrMode: 1" in gm, "VR scene: VR game mode")
for field in ("gameCamera", "planeSelector", "launchController", "flightController", "levelRunner", "hud", "vrRigFollower", "xrOrigin"):
    check(re.search(rf"  {field}: \{{fileID: (?!0\}})-?\d+", gm), "VR scene: " + field + " assigned")

print("PASS: " + str(len(checks)) + " static configuration checks")
for message in checks:
    print("  " + message)
print("Not verified here: compilation, pointer input, rendering, performance, APK installation.")
