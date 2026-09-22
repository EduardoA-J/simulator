#!/usr/bin/env python3
"""Genera modelos OBJ low-poly y texturas PNG para Aviones de Papel VR."""
from __future__ import annotations

import hashlib
import math
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "AvionesPapelVR"


def guid_for(name: str) -> str:
    return hashlib.md5(f"AvionesPapelVR/{name}".encode()).hexdigest()


def write_meta(path: Path, guid: str | None = None, importer: str = "Default") -> None:
    if Path(str(path) + ".meta").exists():
        return  # Preserve Unity GUIDs and authored importer settings.
    g = guid or guid_for(str(path.relative_to(ROOT)).replace("\\", "/"))
    if path.suffix.lower() in {".png", ".jpg", ".jpeg"}:
        content = f"""fileFormatVersion: 2
guid: {g}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 2
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 0
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
  secondaryTextures: []
  nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    elif path.suffix.lower() == ".obj":
        content = f"""fileFormatVersion: 2
guid: {g}
ModelImporter:
  serializedVersion: 24200
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 0
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    motionNodeName: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 1
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.01
    optimizeBones: 1
    meshOptimizationFlags: -1
    generateSecondaryUV: 0
    useFileScale: 1
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{instanceID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  importBlendShapes: 1
  importCameras: 0
  importLights: 0
  nodeNameCollisionStrategy: 1
  fileIdsGeneration: 2
  swapUVChannels: 0
  generateSecondaryUV: 0
  useFileUnits: 1
  keepQuads: 0
  weldVertices: 1
  bakeAxisConversion: 0
  preserveHierarchy: 0
  skinWeightsMode: 0
  maxBonesPerVertex: 4
  minBoneWeight: 0.01
  optimizeBones: 1
  meshOptimizationFlags: -1
  secondaryUVAngleDistortion: 8
  secondaryUVAreaDistortion: 15.000001
  secondaryUVHardAngle: 88
  secondaryUVMarginMethod: 1
  secondaryUVMinLightmapResolution: 40
  secondaryUVMinObjectScale: 1
  secondaryUVPackMargin: 4
  useFileScale: 1
  rescaleMeshes: 1
  remapMaterialsIfMaterialImportModeIsNone: 1
  animationCompression: 1
  importVisibility: 0
  importBlendShapeNormals: 0
  importAnimatedCustomProperties: 0
  importConstraints: 0
  animationPositionError: 0.5
  animationRotationError: 0.5
  animationScaleError: 0.5
  AnimationWrapMode: 0
  extraExposedTransformPaths: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    elif path.suffix.lower() in {".wav", ".mp3", ".ogg"}:
        content = f"""fileFormatVersion: 2
guid: {g}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  preloadAudioData: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    else:
        content = f"""fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    path.with_suffix(path.suffix + ".meta").write_text(content, encoding="utf-8")


class Mesh:
    def __init__(self, name: str):
        self.name = name
        self.verts: list[tuple[float, float, float]] = []
        self.uvs: list[tuple[float, float]] = []
        self.faces: list[list[tuple[int, int]]] = []

    def add_vert(self, x, y, z):
        self.verts.append((x, y, z))
        return len(self.verts)

    def add_uv(self, u, v):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_tri(self, a, b, c, uva=None, uvb=None, uvc=None):
        if uva is None:
            uva = self.add_uv(0, 0)
            uvb = self.add_uv(1, 0)
            uvc = self.add_uv(0.5, 1)
        self.faces.append([(a, uva), (b, uvb), (c, uvc)])

    def add_quad(self, a, b, c, d):
        u0 = self.add_uv(0, 0)
        u1 = self.add_uv(1, 0)
        u2 = self.add_uv(1, 1)
        u3 = self.add_uv(0, 1)
        self.faces.append([(a, u0), (b, u1), (c, u2)])
        self.faces.append([(a, u0), (c, u2), (d, u3)])

    def box(self, cx, cy, cz, sx, sy, sz):
        hx, hy, hz = sx / 2, sy / 2, sz / 2
        ids = []
        for x in (-hx, hx):
            for y in (-hy, hy):
                for z in (-hz, hz):
                    ids.append(self.add_vert(cx + x, cy + y, cz + z))
        # ids: 0(-- -),1(-- +),2(-+-),3(-++),4(+--),5(+-+),6(++-),7(+++)
        faces = [
            (0, 1, 3, 2),
            (4, 6, 7, 5),
            (0, 4, 5, 1),
            (2, 3, 7, 6),
            (0, 2, 6, 4),
            (1, 5, 7, 3),
        ]
        for a, b, c, d in faces:
            self.add_quad(ids[a], ids[b], ids[c], ids[d])

    def cylinder(self, cx, cy, cz, radius, height, segments=10, capped=True):
        top = []
        bot = []
        hy = height / 2
        for i in range(segments):
            ang = 2 * math.pi * i / segments
            x = cx + math.cos(ang) * radius
            z = cz + math.sin(ang) * radius
            top.append(self.add_vert(x, cy + hy, z))
            bot.append(self.add_vert(x, cy - hy, z))
        for i in range(segments):
            j = (i + 1) % segments
            self.add_quad(bot[i], top[i], top[j], bot[j])
        if capped:
            tc = self.add_vert(cx, cy + hy, cz)
            bc = self.add_vert(cx, cy - hy, cz)
            for i in range(segments):
                j = (i + 1) % segments
                self.add_tri(tc, top[j], top[i])
                self.add_tri(bc, bot[i], bot[j])

    def cone(self, cx, cy, cz, radius, height, segments=10):
        tip = self.add_vert(cx, cy + height / 2, cz)
        base = []
        for i in range(segments):
            ang = 2 * math.pi * i / segments
            base.append(
                self.add_vert(
                    cx + math.cos(ang) * radius,
                    cy - height / 2,
                    cz + math.sin(ang) * radius,
                )
            )
        bc = self.add_vert(cx, cy - height / 2, cz)
        for i in range(segments):
            j = (i + 1) % segments
            self.add_tri(tip, base[j], base[i])
            self.add_tri(bc, base[i], base[j])

    def validate(self):
        for face in self.faces:
            a, b, c = [self.verts[index - 1] for index, _ in face]
            u = [b[i] - a[i] for i in range(3)]
            v = [c[i] - a[i] for i in range(3)]
            normal = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
            if sum(n*n for n in normal) < 1e-18:
                raise ValueError(f"Degenerate face in {self.name}: {face}")

    def save(self, path: Path):
        self.validate()
        path.parent.mkdir(parents=True, exist_ok=True)
        lines = [f"o {self.name}", "# Aviones de Papel VR"]
        for x, y, z in self.verts:
            lines.append(f"v {x:.5f} {y:.5f} {z:.5f}")
        for u, v in self.uvs:
            lines.append(f"vt {u:.5f} {v:.5f}")
        lines.append("s off")
        for face in self.faces:
            parts = [f"{vi}/{uv}" for vi, uv in face]
            lines.append("f " + " ".join(parts))
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        write_meta(path)


def paper_plane(name: str, style: int) -> Mesh:
    m = Mesh(name)
    # Classic dart silhouette with style-based wing/body variations
    length = 0.55 + (style % 3) * 0.05
    wing = 0.28 + (style % 4) * 0.03
    body_h = 0.035 + (style % 5) * 0.004
    nose = length * 0.48
    tail = -length * 0.52

    # Nose / body spine
    n = m.add_vert(0, body_h * 0.2, nose)
    spine_mid = m.add_vert(0, body_h, 0)
    spine_tail = m.add_vert(0, body_h * 0.5, tail)
    keel = m.add_vert(0, -body_h, -length * 0.05)

    # Wings
    lw = m.add_vert(-wing, 0, tail * 0.15)
    rw = m.add_vert(wing, 0, tail * 0.15)
    lw_tip = m.add_vert(-wing * 1.05, body_h * 0.4, tail * 0.55)
    rw_tip = m.add_vert(wing * 1.05, body_h * 0.4, tail * 0.55)
    lw_rear = m.add_vert(-wing * 0.55, 0, tail)
    rw_rear = m.add_vert(wing * 0.55, 0, tail)

    # Closed paper shell: top faces point up, matching bottom outline points down.
    outline = [n, lw, lw_tip, lw_rear, spine_tail, rw_rear, rw_tip, rw]
    for i, a in enumerate(outline):
        b = outline[(i + 1) % len(outline)]
        m.add_tri(spine_mid, b, a)
        m.add_tri(keel, a, b)

    extras_start = len(m.faces)
    # Style extras: canards / twin tails / delta tips
    if style % 3 == 1:
        cl = m.add_vert(-wing * 0.35, body_h * 0.2, nose * 0.35)
        cr = m.add_vert(wing * 0.35, body_h * 0.2, nose * 0.35)
        m.add_tri(n, cl, spine_mid)
        m.add_tri(n, spine_mid, cr)
    if style % 3 == 2:
        fin = m.add_vert(0, body_h * 2.2, tail * 0.7)
        m.add_tri(spine_tail, rw_rear, fin)
        m.add_tri(spine_tail, fin, lw_rear)
    m.faces.extend([list(reversed(face)) for face in m.faces[extras_start:]])
    if style >= 5:
        # Folded belly thicken
        m.box(0, -body_h * 0.6, -0.05, 0.04, body_h, length * 0.55)
    return m


def weapon_bullet() -> Mesh:
    m = Mesh("PaperBullet")
    m.cylinder(0, 0, 0, 0.02, 0.08, 8)
    m.cone(0, 0.06, 0, 0.02, 0.04, 8)
    return m


def weapon_missile() -> Mesh:
    m = Mesh("FoldMissile")
    m.cylinder(0, 0, 0, 0.035, 0.18, 8)
    m.cone(0, 0.12, 0, 0.035, 0.08, 8)
    m.box(-0.05, -0.04, 0, 0.08, 0.02, 0.06)
    m.box(0.05, -0.04, 0, 0.08, 0.02, 0.06)
    m.box(0, -0.04, -0.05, 0.02, 0.06, 0.08)
    return m


def weapon_bomb() -> Mesh:
    m = Mesh("InkBomb")
    # Drop-like
    m.cylinder(0, 0, 0, 0.05, 0.1, 10)
    m.cone(0, 0.08, 0, 0.05, 0.06, 10)
    m.box(0, -0.07, 0, 0.02, 0.04, 0.02)
    return m


def weapon_shield() -> Mesh:
    m = Mesh("OrigamiShield")
    # Hexagonal disc
    center = m.add_vert(0, 0, 0)
    ring = []
    for i in range(6):
        ang = 2 * math.pi * i / 6
        ring.append(m.add_vert(math.cos(ang) * 0.18, 0.01, math.sin(ang) * 0.18))
    for i in range(6):
        j = (i + 1) % 6
        m.add_tri(center, ring[i], ring[j])
        m.add_tri(center, ring[j], ring[i])  # back
    return m


def enemy_drone() -> Mesh:
    m = Mesh("HostileDrone")
    m.box(0, 0, 0, 0.25, 0.08, 0.25)
    for x, z in [(-0.2, -0.2), (-0.2, 0.2), (0.2, -0.2), (0.2, 0.2)]:
        m.cylinder(x, 0.06, z, 0.07, 0.02, 8)
        m.box(x, 0.02, z, 0.02, 0.04, 0.02)
    m.cone(0, -0.06, 0, 0.04, 0.06, 6)
    return m


def enemy_bird() -> Mesh:
    m = Mesh("MechBird")
    m.box(0, 0, 0, 0.08, 0.06, 0.22)
    m.cone(0, 0, 0.14, 0.03, 0.08, 6)
    m.box(-0.16, 0.02, 0, 0.22, 0.015, 0.1)
    m.box(0.16, 0.02, 0, 0.22, 0.015, 0.1)
    m.box(0, 0.08, -0.08, 0.02, 0.1, 0.06)
    return m


def enemy_fan() -> Mesh:
    m = Mesh("WindFan")
    m.cylinder(0, 0, 0, 0.06, 0.2, 10)
    m.box(0, 0.12, 0, 0.35, 0.02, 0.06)
    m.box(0, 0.12, 0, 0.06, 0.02, 0.35)
    m.box(0, -0.12, 0, 0.16, 0.04, 0.16)
    return m


def prop_table() -> Mesh:
    m = Mesh("WoodTable")
    m.box(0, 0.74, 0, 1.4, 0.06, 0.8)
    for x, z in [(-0.6, -0.32), (-0.6, 0.32), (0.6, -0.32), (0.6, 0.32)]:
        m.box(x, 0.35, z, 0.07, 0.7, 0.07)
    return m


def prop_desk() -> Mesh:
    m = Mesh("SchoolDesk")
    m.box(0, 0.7, 0, 0.7, 0.04, 0.45)
    m.box(0, 0.35, -0.18, 0.65, 0.66, 0.04)
    for x in (-0.28, 0.28):
        m.box(x, 0.35, 0.18, 0.05, 0.7, 0.05)
    return m


def prop_board() -> Mesh:
    m = Mesh("Chalkboard")
    m.box(0, 1.2, 0, 1.8, 1.1, 0.05)
    m.box(0, 0.55, 0, 1.9, 0.08, 0.08)
    return m


def prop_tree() -> Mesh:
    m = Mesh("ParkTree")
    m.cylinder(0, 0.5, 0, 0.08, 1.0, 8)
    m.cone(0, 1.4, 0, 0.45, 0.9, 8)
    m.cone(0, 1.85, 0, 0.32, 0.7, 8)
    return m


def prop_bench() -> Mesh:
    m = Mesh("ParkBench")
    m.box(0, 0.42, 0, 1.2, 0.06, 0.4)
    m.box(0, 0.7, -0.18, 1.2, 0.45, 0.05)
    for x in (-0.5, 0.5):
        m.box(x, 0.2, 0, 0.06, 0.4, 0.35)
    return m


def prop_fountain() -> Mesh:
    m = Mesh("Fountain")
    m.cylinder(0, 0.15, 0, 0.7, 0.3, 12)
    m.cylinder(0, 0.45, 0, 0.35, 0.3, 10)
    m.cylinder(0, 0.85, 0, 0.08, 0.5, 8)
    return m


def prop_building() -> Mesh:
    m = Mesh("MiniBuilding")
    m.box(0, 0.75, 0, 0.6, 1.5, 0.5)
    m.box(0, 1.6, 0, 0.65, 0.1, 0.55)
    return m


def prop_crystal() -> Mesh:
    m = Mesh("MagicCrystal")
    tip = m.add_vert(0, 0.35, 0)
    bot = m.add_vert(0, -0.2, 0)
    ring = []
    for i in range(5):
        ang = 2 * math.pi * i / 5
        ring.append(m.add_vert(math.cos(ang) * 0.12, 0.05, math.sin(ang) * 0.12))
    for i in range(5):
        j = (i + 1) % 5
        m.add_tri(tip, ring[i], ring[j])
        m.add_tri(bot, ring[j], ring[i])
    return m


def collectible_star() -> Mesh:
    m = Mesh("StarCollectible")
    pts = []
    for i in range(10):
        ang = -math.pi / 2 + i * math.pi / 5
        r = 0.12 if i % 2 == 0 else 0.05
        pts.append(m.add_vert(math.cos(ang) * r, math.sin(ang) * r, 0))
    c = m.add_vert(0, 0, 0.02)
    c2 = m.add_vert(0, 0, -0.02)
    for i in range(10):
        j = (i + 1) % 10
        m.add_tri(c, pts[i], pts[j])
        m.add_tri(c2, pts[j], pts[i])
    return m


def collectible_coin() -> Mesh:
    m = Mesh("PaperCoin")
    m.cylinder(0, 0, 0, 0.08, 0.015, 12)
    return m


def collectible_part() -> Mesh:
    m = Mesh("PlanePart")
    m.box(0, 0, 0, 0.12, 0.02, 0.06)
    m.box(0.04, 0.02, 0, 0.04, 0.04, 0.04)
    return m


def obstacle_static() -> Mesh:
    m = Mesh("StaticObstacle")
    m.box(0, 0.5, 0, 0.4, 1.0, 0.4)
    return m


def obstacle_moving() -> Mesh:
    m = Mesh("MovingObstacle")
    m.box(0, 0, 0, 0.5, 0.2, 0.2)
    m.box(0, 0, 0, 0.2, 0.2, 0.5)
    return m


def powerup_orb(name: str) -> Mesh:
    m = Mesh(name)
    # Low-poly icosa-ish
    t = (1.0 + math.sqrt(5.0)) / 2.0
    verts = [
        (-1, t, 0),
        (1, t, 0),
        (-1, -t, 0),
        (1, -t, 0),
        (0, -1, t),
        (0, 1, t),
        (0, -1, -t),
        (0, 1, -t),
        (t, 0, -1),
        (t, 0, 1),
        (-t, 0, -1),
        (-t, 0, 1),
    ]
    scale = 0.08
    ids = []
    for x, y, z in verts:
        n = math.sqrt(x * x + y * y + z * z)
        ids.append(m.add_vert(x / n * scale, y / n * scale, z / n * scale))
    faces = [
        (0, 11, 5),
        (0, 5, 1),
        (0, 1, 7),
        (0, 7, 10),
        (0, 10, 11),
        (1, 5, 9),
        (5, 11, 4),
        (11, 10, 2),
        (10, 7, 6),
        (7, 1, 8),
        (3, 9, 4),
        (3, 4, 2),
        (3, 2, 6),
        (3, 6, 8),
        (3, 8, 9),
        (4, 9, 5),
        (2, 4, 11),
        (6, 2, 10),
        (8, 6, 7),
        (9, 8, 1),
    ]
    for a, b, c in faces:
        m.add_tri(ids[a], ids[b], ids[c])
    return m


# --- Textures ---

def noise(img: Image.Image, amount=18):
    import random

    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            d = random.randint(-amount, amount)
            px[x, y] = (
                max(0, min(255, r + d)),
                max(0, min(255, g + d)),
                max(0, min(255, b + d)),
                a,
            )
    return img


def tex_wood(path: Path, size=512):
    img = Image.new("RGBA", (size, size), (140, 94, 55, 255))
    d = ImageDraw.Draw(img)
    for i in range(0, size, 8):
        shade = 110 + (i * 7) % 40
        d.line([(0, i), (size, i + 18)], fill=(shade, 70 + i % 20, 40, 255), width=3)
    noise(img, 12)
    img = img.filter(ImageFilter.SMOOTH)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    write_meta(path)


def tex_paper(path: Path, color, size=512, lines=True):
    img = Image.new("RGBA", (size, size), (*color, 255))
    d = ImageDraw.Draw(img)
    if lines:
        for y in range(40, size, 28):
            d.line([(20, y), (size - 20, y)], fill=(180, 200, 220, 90), width=1)
    # fold creases
    d.line([(size // 2, 0), (size // 2, size)], fill=(0, 0, 0, 35), width=2)
    d.line([(0, size // 2), (size, size // 3)], fill=(0, 0, 0, 25), width=1)
    noise(img, 10)
    img.save(path)
    write_meta(path)


def tex_solid(path: Path, color, size=256, glow=False):
    img = Image.new("RGBA", (size, size), (*color, 255))
    if glow:
        d = ImageDraw.Draw(img)
        for r in range(size // 2, 10, -8):
            a = int(40 + (size // 2 - r) * 1.5)
            c = (*color[:3], min(255, a))
            d.ellipse([size // 2 - r, size // 2 - r, size // 2 + r, size // 2 + r], fill=c)
    noise(img, 8)
    img.save(path)
    write_meta(path)


def tex_metal(path: Path, size=512):
    img = Image.new("RGBA", (size, size), (90, 100, 115, 255))
    d = ImageDraw.Draw(img)
    for i in range(0, size, 16):
        d.rectangle([i, 0, i + 8, size], fill=(110, 120, 135, 255))
    noise(img, 15)
    img.save(path)
    write_meta(path)


def tex_grass(path: Path, size=512):
    img = Image.new("RGBA", (size, size), (70, 130, 60, 255))
    noise(img, 25)
    img.save(path)
    write_meta(path)


def tex_starry(path: Path, size=512):
    import random

    img = Image.new("RGBA", (size, size), (12, 16, 48, 255))
    d = ImageDraw.Draw(img)
    for _ in range(180):
        x, y = random.randint(0, size - 1), random.randint(0, size - 1)
        r = random.randint(1, 3)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 230, 255))
    img.save(path)
    write_meta(path)


def write_wav_tone(path: Path, freq: float, duration=0.25, volume=0.3):
    """WAV PCM 16-bit mono placeholder for 3D SFX."""
    import struct
    import wave

    rate = 22050
    n = int(rate * duration)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            env = max(0.0, 1.0 - t / duration)
            sample = int(volume * env * 32767 * math.sin(2 * math.pi * freq * t))
            frames += struct.pack("<h", sample)
        w.writeframes(frames)
    write_meta(path)


def ensure_folder_metas():
    folders = [
        ROOT,
        ROOT / "Models",
        ROOT / "Models" / "Planes",
        ROOT / "Models" / "Weapons",
        ROOT / "Models" / "Enemies",
        ROOT / "Models" / "Props",
        ROOT / "Models" / "Collectibles",
        ROOT / "Models" / "Environments",
        ROOT / "Textures",
        ROOT / "Materials",
        ROOT / "Prefabs",
        ROOT / "Prefabs" / "Planes",
        ROOT / "Prefabs" / "Weapons",
        ROOT / "Prefabs" / "Enemies",
        ROOT / "Prefabs" / "Props",
        ROOT / "Prefabs" / "Collectibles",
        ROOT / "Prefabs" / "Environments",
        ROOT / "Data",
        ROOT / "Audio",
        ROOT / "Scripts",
        ROOT / "Scripts" / "Core",
        ROOT / "Scripts" / "Gameplay",
        ROOT / "Scripts" / "Editor",
        ROOT / "Scenes",
    ]
    for f in folders:
        f.mkdir(parents=True, exist_ok=True)
        meta = f.with_suffix(f.suffix + ".meta") if False else Path(str(f) + ".meta")
        if not meta.exists():
            write_meta(f)


def main(models_only=False):
    ensure_folder_metas()

    plane_names = [
        "DartClassic",
        "GliderLong",
        "ArrowSwift",
        "DeltaRacer",
        "CanardAce",
        "HeavyBomber",
        "ZigZag",
        "NightHawk",
        "OrigamiPhoenix",
        "StormCutter",
        "PaperFalcon",
        "CloudSkimmer",
    ]
    for i, name in enumerate(plane_names):
        paper_plane(name, i).save(ROOT / "Models" / "Planes" / f"{name}.obj")

    weapon_bullet().save(ROOT / "Models" / "Weapons" / "PaperBullet.obj")
    weapon_missile().save(ROOT / "Models" / "Weapons" / "FoldMissile.obj")
    weapon_bomb().save(ROOT / "Models" / "Weapons" / "InkBomb.obj")
    weapon_shield().save(ROOT / "Models" / "Weapons" / "OrigamiShield.obj")

    enemy_drone().save(ROOT / "Models" / "Enemies" / "HostileDrone.obj")
    enemy_bird().save(ROOT / "Models" / "Enemies" / "MechBird.obj")
    enemy_fan().save(ROOT / "Models" / "Enemies" / "WindFan.obj")

    prop_table().save(ROOT / "Models" / "Props" / "WoodTable.obj")
    prop_desk().save(ROOT / "Models" / "Props" / "SchoolDesk.obj")
    prop_board().save(ROOT / "Models" / "Props" / "Chalkboard.obj")
    prop_tree().save(ROOT / "Models" / "Props" / "ParkTree.obj")
    prop_bench().save(ROOT / "Models" / "Props" / "ParkBench.obj")
    prop_fountain().save(ROOT / "Models" / "Props" / "Fountain.obj")
    prop_building().save(ROOT / "Models" / "Props" / "MiniBuilding.obj")
    prop_crystal().save(ROOT / "Models" / "Props" / "MagicCrystal.obj")
    obstacle_static().save(ROOT / "Models" / "Props" / "StaticObstacle.obj")
    obstacle_moving().save(ROOT / "Models" / "Props" / "MovingObstacle.obj")

    collectible_star().save(ROOT / "Models" / "Collectibles" / "StarCollectible.obj")
    collectible_coin().save(ROOT / "Models" / "Collectibles" / "PaperCoin.obj")
    collectible_part().save(ROOT / "Models" / "Collectibles" / "PlanePart.obj")
    for name in ("TurboOrb", "ShieldOrb", "TripleShotOrb"):
        powerup_orb(name).save(ROOT / "Models" / "Collectibles" / f"{name}.obj")

    # Environment modular tiles
    floor = Mesh("FloorTile")
    floor.box(0, 0, 0, 2, 0.05, 2)
    floor.save(ROOT / "Models" / "Environments" / "FloorTile.obj")
    wall = Mesh("WallTile")
    wall.box(0, 1, 0, 2, 2, 0.1)
    wall.save(ROOT / "Models" / "Environments" / "WallTile.obj")

    if models_only:
        return

    # Textures
    tex_wood(ROOT / "Textures" / "WoodTable.png")
    tex_paper(ROOT / "Textures" / "PaperWhite.png", (245, 242, 230))
    tex_paper(ROOT / "Textures" / "PaperBlue.png", (170, 200, 240))
    tex_paper(ROOT / "Textures" / "PaperRed.png", (235, 120, 110))
    tex_paper(ROOT / "Textures" / "PaperYellow.png", (245, 220, 110))
    tex_paper(ROOT / "Textures" / "PaperGreen.png", (150, 210, 150))
    tex_paper(ROOT / "Textures" / "PaperPurple.png", (190, 150, 220))
    tex_paper(ROOT / "Textures" / "PaperBlack.png", (40, 42, 48), lines=False)
    tex_paper(ROOT / "Textures" / "PaperOrange.png", (240, 160, 80))
    tex_paper(ROOT / "Textures" / "PaperCyan.png", (140, 220, 230))
    tex_paper(ROOT / "Textures" / "PaperPink.png", (240, 170, 200))
    tex_paper(ROOT / "Textures" / "PaperGold.png", (230, 190, 90))
    tex_metal(ROOT / "Textures" / "MetalDrone.png")
    tex_grass(ROOT / "Textures" / "Grass.png")
    tex_starry(ROOT / "Textures" / "StarrySky.png")
    tex_solid(ROOT / "Textures" / "InkBlack.png", (20, 20, 30))
    tex_solid(ROOT / "Textures" / "ShieldCyan.png", (80, 220, 230), glow=True)
    tex_solid(ROOT / "Textures" / "StarYellow.png", (255, 220, 60), glow=True)
    tex_solid(ROOT / "Textures" / "CoinGold.png", (230, 180, 50))
    tex_solid(ROOT / "Textures" / "TurboOrange.png", (255, 120, 40), glow=True)
    tex_solid(ROOT / "Textures" / "TripleMagenta.png", (220, 60, 180), glow=True)
    tex_solid(ROOT / "Textures" / "ChalkGreen.png", (40, 90, 55))
    tex_solid(ROOT / "Textures" / "FuturOffice.png", (60, 80, 110))
    tex_solid(ROOT / "Textures" / "MagicPurple.png", (120, 70, 180), glow=True)

    # Audio placeholders (3D)
    write_wav_tone(ROOT / "Audio" / "sfx_shoot.wav", 880, 0.12, 0.25)
    write_wav_tone(ROOT / "Audio" / "sfx_wind.wav", 120, 0.8, 0.15)
    write_wav_tone(ROOT / "Audio" / "sfx_collision.wav", 90, 0.2, 0.35)
    write_wav_tone(ROOT / "Audio" / "sfx_explosion_ink.wav", 60, 0.45, 0.4)
    write_wav_tone(ROOT / "Audio" / "sfx_pickup.wav", 1200, 0.18, 0.25)
    write_wav_tone(ROOT / "Audio" / "sfx_launch.wav", 300, 0.35, 0.3)

    print("Assets generated under", ROOT)


if __name__ == "__main__":
    import argparse
    import shutil
    from datetime import datetime
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Generate outside Assets for inspection")
    parser.add_argument("--overwrite", action="store_true", help="Back up existing output and regenerate")
    parser.add_argument("--models-only", action="store_true")
    args = parser.parse_args()
    if args.output:
        ROOT = args.output.resolve()
    if (ROOT / "Models").exists():
        if not args.overwrite:
            parser.error("Models already exists. Use --output for a new directory or --overwrite with backup.")
        backup = Path(__file__).resolve().parents[1] / "Backups" / "Generator" / datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        shutil.copytree(ROOT, backup)
        print("Backup:", backup)
    main(models_only=args.models_only)
