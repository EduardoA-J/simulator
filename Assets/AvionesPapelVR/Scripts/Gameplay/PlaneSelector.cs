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
            if (_table == null) _table = GameObject.Find("Lobby_WoodTable");
            if (_table == null) _table = GameObject.Find("Lobby_Table");
            if (_table == null && GameManager.Instance.tablePrefab != null)
                _table = Instantiate(GameManager.Instance.tablePrefab);
            if (_table != null)
                _table.transform.position = tableAnchor.position - Vector3.up * 0.77f;

            for (int i = 0; i < _defs.Count; i++)
            {
                var def = _defs[i];
                if (def == null || def.prefab == null) { _planes.Add(null); continue; }
                var go = Instantiate(def.prefab, tableAnchor);
                go.name = "Select_" + def.displayName;
                int slot = i % 4;
                go.transform.localPosition = new Vector3((slot % 2 - 0.5f) * 0.5f, 0.025f, (slot / 2 - 0.5f) * 0.38f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * Mathf.Min(displayScale.x, 0.35f);
                foreach (var demo in go.GetComponentsInChildren<PlaneFlightDemo>()) { demo.enabled = false; Destroy(demo); }
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
                    if (rb.gameObject != go) Destroy(rb);
                if (enableVrGrab && GameManager.Instance.IsPlaneUnlocked(def))
                {
                    var grab = go.AddComponent<VrGrabPlane>();
                    grab.Arm(def);
                }
                else
                {
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
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

        public PlaneDefinition Current => _defs != null && _defs.Count > 0 ? _defs[_index] : null;
        public void Clear()
        {
            _active = _holding = false;
            foreach (var go in _spawned)
                if (go != null) { go.SetActive(false); Destroy(go); }
            _spawned.Clear();
            _planes.Clear();
        }
    }
}
