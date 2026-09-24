"""Read-only asset checks; this does not replace Unity compilation or headset testing."""
from pathlib import Path
import re

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
check("m_AutomaticallyInstantiateSimulatorPrefab: 0" in read(
    "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset"), "No automatic simulated controllers")
scene = read("Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity")
gm = next(b for b in scene.split("---") if "Assembly-CSharp::AvionesPapelVR.GameManager" in b)
check("vrMode: 1" in gm, "VR scene: VR game mode")
for field in ("gameCamera", "planeSelector", "launchController", "flightController", "levelRunner", "hud", "vrRigFollower", "xrOrigin"):
    check(re.search(rf"  {field}: \{{fileID: (?!0\}})-?\d+", gm), "VR scene: " + field + " assigned")

print("PASS: " + str(len(checks)) + " static configuration checks")
for message in checks:
    print("  " + message)
print("Not verified here: compilation, pointer input, rendering, performance, APK installation.")
