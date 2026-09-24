using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AvionesPapelVR
{
    public class PlaneSelector : MonoBehaviour
    {
        public Transform tableAnchor;
        public float spacing = 0.42f;
        public Vector3 displayScale = new Vector3(0.35f, 0.35f, 0.35f);
        public bool enableVrGrab = true;
        readonly List<GameObject> _spawned = new();
        readonly List<GameObject> _planes = new();
        List<PlaneDefinition> _defs;
        int _index;
        System.Action<PlaneDefinition> _onChosen;
        bool _active, _holding;
        float _stickCooldown;
        GameObject _table;
        GameObject _lobby;
        readonly List<Material> _lobbyMaterials = new();
        public int Page => _index / 4 + 1;
        public int PageCount => _defs == null ? 0 : Mathf.CeilToInt(_defs.Count / 4f);

        public void Begin(List<PlaneDefinition> planes, System.Action<PlaneDefinition> onChosen)
        {
            Clear();
            _defs = planes;
            _onChosen = onChosen;
            if (_defs == null || _defs.Count == 0 || tableAnchor == null) return;
            _index = Mathf.Max(0, _defs.FindIndex(d => GameManager.Instance.IsPlaneUnlocked(d)));
            _active = true;
            _holding = false;
            PrepareLobby();

            for (int i = 0; i < _defs.Count; i++)
            {
                var def = _defs[i];
                if (def == null || def.prefab == null) { _planes.Add(null); continue; }
                var go = Instantiate(def.prefab, tableAnchor);
                go.name = "Select_" + def.displayName;
                int slot = i % 4;
                go.transform.localPosition = new Vector3((slot % 2 - 0.5f) * 0.5f, 0.005f, (slot / 2 - 0.5f) * 0.28f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * Mathf.Min(displayScale.x, 0.35f);
                foreach (var demo in go.GetComponentsInChildren<PlaneFlightDemo>()) { demo.enabled = false; Destroy(demo); }
                foreach (var spin in go.GetComponentsInChildren<SpinBob>()) { spin.enabled = false; Destroy(spin); }
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
                    if (rb.gameObject != go) Destroy(rb);
                // Todo avión expuesto es un cuerpo cinemático en reposo; sólo los desbloqueados se pueden agarrar.
                var body = go.GetComponent<Rigidbody>();
                if (body == null) body = go.AddComponent<Rigidbody>();
                VrGrabPlane.ConfigureTableBody(body);
                if (enableVrGrab && GameManager.Instance.IsPlaneUnlocked(def))
                {
                    var grab = go.AddComponent<VrGrabPlane>();
                    grab.Arm(def);
                }
                _planes.Add(go);
                _spawned.Add(go);
            }
            ShowPage();
        }

        void Update()
        {
            if (!_active || _holding || _defs == null || _defs.Count == 0) return;
            _stickCooldown -= Time.deltaTime;
            Vector2 stick = VrInput.Stick(XRNode.LeftHand);
            int direction = 0;
            if (_stickCooldown <= 0f && Mathf.Abs(stick.x) > 0.55f) direction = stick.x < 0f ? -1 : 1;
            if (GameInput.IsDown(Key.LeftArrow)) direction = -1;
            if (GameInput.IsDown(Key.RightArrow)) direction = 1;
            if (direction != 0)
            {
                _index = (_index + direction + _defs.Count) % _defs.Count;
                _stickCooldown = 0.28f;
                ShowPage();
            }
            if (!enableVrGrab && GameInput.ConfirmDown()) ConfirmCurrent();
        }

        public void ConfirmCurrent()
        {
            if (!_active || Current == null || !GameManager.Instance.IsPlaneUnlocked(Current)) return;
            var chosen = Current;
            Clear();
            _onChosen?.Invoke(chosen);
        }

        void ShowPage()
        {
            for (int i = 0; i < _planes.Count; i++)
                if (_planes[i] != null) _planes[i].SetActive(i / 4 == _index / 4);
            GameManager.Instance?.hud?.Refresh();
        }

        public void Browse(int direction)
        {
            if (!_active || _holding || _defs == null || _defs.Count == 0) return;
            _index = (_index + direction + _defs.Count) % _defs.Count;
            ShowPage();
        }

        public void SelectGrabbed(PlaneDefinition definition)
        {
            if (_defs == null) return;
            int index = _defs.IndexOf(definition);
            if (index < 0) return;
            _index = index;
            _holding = true;
            foreach (var plane in _planes)
            {
                if (plane == null) continue;
                var grab = plane.GetComponent<XRGrabInteractable>();
                if (grab != null && !grab.isSelected) grab.enabled = false;
            }
        }

        public void ReturnToTable()
        {
            if (!_active) return;
            _holding = false;
            foreach (var plane in _planes)
            {
                if (plane == null) continue;
                var grab = plane.GetComponent<XRGrabInteractable>();
                if (grab != null) grab.enabled = true;
            }
        }

        /// <summary>Devuelve todos los aviones expuestos a su pose de mesa (sin agarre ni velocidad).</summary>
        public void ResetTable()
        {
            _holding = false;
            foreach (var plane in _planes)
            {
                if (plane == null) continue;
                var grab = plane.GetComponent<VrGrabPlane>();
                if (grab != null && grab.IsArmed) grab.ResetToTable();
            }
        }

        public PlaneDefinition Current => _defs != null && _defs.Count > 0 ? _defs[_index] : null;

        public void PrepareLobby()
        {
            if (tableAnchor == null || _lobby != null) return;
            if (_table == null) _table = GameObject.Find("Lobby_WoodTable") ?? GameObject.Find("Lobby_Table");
            if (_table == null && GameManager.Instance.tablePrefab != null)
                _table = Instantiate(GameManager.Instance.tablePrefab);
            float top = tableAnchor.position.y - 0.08f;
            if (_table != null)
            {
                // The existing WoodTable mesh is 0.77 m tall; scale the legs down to the floor.
                _table.transform.SetPositionAndRotation(new Vector3(tableAnchor.position.x, 0f, tableAnchor.position.z), tableAnchor.rotation);
                _table.transform.localScale = new Vector3(1f, top / 0.77f, 0.875f);
            }
            _lobby = new GameObject("Lobby_Workshop");
            _lobby.transform.SetPositionAndRotation(new Vector3(tableAnchor.position.x, 0f, tableAnchor.position.z), tableAnchor.rotation);
            var dark = LobbyMaterial(new Color(0.12f, 0.19f, 0.23f));
            var teal = LobbyMaterial(new Color(0.18f, 0.66f, 0.67f));
            var paper = LobbyMaterial(new Color(0.91f, 0.85f, 0.69f));
            var amber = LobbyMaterial(new Color(0.9f, 0.48f, 0.18f));
            LobbyProp("WorkshopMat", new Vector3(0, 0.012f, -0.1f), new Vector3(2.6f, 0.015f, 2.3f), dark);
            // Thin, non-interactive pads frame the aircraft without covering the grip area.
            for (int i = 0; i < 4; i++)
                LobbyProp("PlanePad", new Vector3((i % 2 - 0.5f) * 0.5f, top + 0.005f, (i / 2 - 0.5f) * 0.28f),
                    new Vector3(0.42f, 0.008f, 0.25f), teal);
            LobbyProp("LaunchMark", new Vector3(0, top + 0.014f, 0.23f), new Vector3(0.025f, 0.01f, 0.18f), paper);
            foreach (float side in new[] { -1f, 1f })
            {
                var arrow = LobbyProp("LaunchArrow", new Vector3(side * 0.035f, top + 0.014f, 0.27f),
                    new Vector3(0.02f, 0.01f, 0.1f), paper);
                arrow.localRotation = Quaternion.Euler(0, side * -45f, 0);
            }
            LobbyProp("SideShelf", new Vector3(1.65f, 0.4f, 0.9f), new Vector3(0.85f, 0.8f, 0.55f), dark);
            for (int i = 0; i < 3; i++)
                LobbyProp("OversizedNotebook", new Vector3(1.4f + i * 0.22f, 1.15f, 0.9f),
                    new Vector3(0.15f, 0.7f + i * 0.12f, 0.48f), i % 2 == 0 ? paper : teal);
            LobbyProp("LampBase", new Vector3(-0.98f, 0.035f, 0.6f), new Vector3(0.38f, 0.07f, 0.38f), dark);
            LobbyProp("LampStand", new Vector3(-0.98f, 1.2f, 0.6f), new Vector3(0.045f, 2.4f, 0.045f), amber);
            LobbyProp("LampArm", new Vector3(-0.58f, 2.4f, 0.6f), new Vector3(0.85f, 0.045f, 0.045f), amber);
            LobbyProp("LampShade", new Vector3(-0.18f, 2.32f, 0.6f), new Vector3(0.28f, 0.12f, 0.25f), paper);
            var lightGo = new GameObject("TableLight");
            lightGo.transform.SetParent(_lobby.transform, false);
            lightGo.transform.localPosition = new Vector3(-0.18f, 2.2f, 0.5f);
            lightGo.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot; light.range = 4f; light.spotAngle = 90f;
            light.intensity = 2f; light.color = new Color(1f, 0.88f, 0.7f); light.shadows = LightShadows.None;
        }

        Material LobbyMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color; material.SetFloat("_Smoothness", 0.2f);
            _lobbyMaterials.Add(material);
            return material;
        }

        Transform LobbyProp(string label, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label; go.transform.SetParent(_lobby.transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Collider>().enabled = false;
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        void OnDestroy()
        {
            if (_lobby != null) Destroy(_lobby);
            foreach (var material in _lobbyMaterials) if (material != null) Destroy(material);
        }

        public void Clear()
        {
            _active = _holding = false;
            // Retirar antes de desactivar: ningún avión debe lanzar ni notificar mientras se desmonta la mesa.
            foreach (var go in _spawned)
                if (go != null && go.TryGetComponent(out VrGrabPlane grab)) grab.Retire();
            foreach (var go in _spawned)
                if (go != null) { go.SetActive(false); Destroy(go); }
            _spawned.Clear();
            _planes.Clear();
        }
    }
}
