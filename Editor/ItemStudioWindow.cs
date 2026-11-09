using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS.ItemsEditor
{
    /// <summary>
    /// Tools > Horror FPS > 10. Item Studio
    /// Janela de itens: lista de todos os ItemData, edição dos campos e um estúdio de fotografia 3D.
    /// Mostra o modelo do item (modelo de inspeção ou do mundo), você gira/aproxima/arrasta até achar um bom ângulo e
    /// clica em "Tirar foto": o PNG com fundo transparente é salvo, vira Sprite e é ligado ao campo Icon do item.
    /// A foto sai exatamente com a proporção da caixa da forma do item (ex.: 2×1 células = imagem 2:1), então encaixa
    /// perfeito na grade do inventário. O ângulo/zoom/luz ficam guardados no item para refazer a foto depois.
    ///
    /// Controles no modelo:  botão esquerdo = girar • botão direito/meio (ou Alt+esquerdo) = mover • roda = zoom • duplo clique = enquadrar.
    /// </summary>
    public class ItemStudioWindow : EditorWindow
    {
        const string SelKey = "HorrorFPS.ItemStudio.Selected";
        const string FolderKey = "HorrorFPS.ItemStudio.IconsFolder";
        const string BgKey = "HorrorFPS.ItemStudio.Bg";
        const string TabKey = "HorrorFPS.ItemStudio.Tab";
        const float LeftW = 230f;
        const float RightW = 340f;
        const float TopH = 22f;
        const float BarH = 40f;
        const int MaxPixels = 2048;

        static readonly string[] SourceNames = { "Inspeção (ou mundo)", "Mundo (ou inspeção)" };
        static readonly string[] BgNames = { "Escuro", "Cinza", "Claro", "Verde (chroma)" };
        static readonly Color[] BgColors =
        {
            new Color(0.13f, 0.13f, 0.14f, 1f), new Color(0.38f, 0.38f, 0.40f, 1f),
            new Color(0.82f, 0.82f, 0.84f, 1f), new Color(0.1f, 0.55f, 0.2f, 1f)
        };
        static readonly int[] PixelOptions = { 32, 48, 64, 96, 128, 192, 256 };
        static readonly string[] PixelNames = { "32 px", "48 px", "64 px", "96 px", "128 px", "192 px", "256 px" };

        // ---------------------------------------------------------------- estado
        List<ItemData> all = new List<ItemData>();
        ItemData item;
        string search = "";
        Vector2 listScroll, panelScroll;
        int tab;                       // 0 = Foto, 1 = Item
        int bg;
        string iconsFolder;
        bool showGrid = true;
        int invRot;                    // rotação da prévia "no inventário"

        // prévia 3D
        PreviewRenderUtility preview;
        GameObject loadedPrefab;
        GameObject instance;
        readonly List<Vector3> points = new List<Vector3>();
        Vector3 center;
        float radius = 0.5f;
        int dragButton;
        bool dragAlt;
        bool shotDirty;

        Editor itemEditor;

        [MenuItem("Tools/Horror FPS/10. Item Studio")]
        static void Open()
        {
            ItemStudioWindow w = GetWindow<ItemStudioWindow>("Item Studio");
            w.minSize = new Vector2(980f, 560f);
            w.Show();
        }

        /// <summary>Abre o estúdio já com este item selecionado.</summary>
        public static void OpenFor(ItemData data)
        {
            ItemStudioWindow w = GetWindow<ItemStudioWindow>("Item Studio");
            w.minSize = new Vector2(980f, 560f);
            w.Show();
            if (data != null) w.Select(data);
        }

        // ---------------------------------------------------------------- ciclo de vida
        void OnEnable()
        {
            tab = SessionState.GetInt(TabKey, 0);
            bg = Mathf.Clamp(EditorPrefs.GetInt(BgKey, 0), 0, BgColors.Length - 1);
            iconsFolder = EditorPrefs.GetString(FolderKey, "");
            if (string.IsNullOrEmpty(iconsFolder)) iconsFolder = DefaultIconsFolder();
            RefreshList();
            string guid = SessionState.GetString(SelKey, "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            }
            if (item == null && all.Count > 0) item = all[0];
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            SaveNow();
            DestroyModel();
            if (preview != null) { preview.Cleanup(); preview = null; }
            if (itemEditor != null) { DestroyImmediate(itemEditor); itemEditor = null; }
        }

        void OnLostFocus() { SaveNow(); }
        void OnProjectChange() { RefreshList(); Repaint(); }
        void OnFocus() { RefreshList(); }
        void OnUndo() { Repaint(); }

        void SaveNow()
        {
            if (!shotDirty) return;
            shotDirty = false;
            AssetDatabase.SaveAssets();
        }

        string DefaultIconsFolder()
        {
            try
            {
                MonoScript ms = MonoScript.FromScriptableObject(this);
                string p = ms != null ? AssetDatabase.GetAssetPath(ms) : null;
                if (!string.IsNullOrEmpty(p))
                {
                    string root = Path.GetDirectoryName(Path.GetDirectoryName(p));
                    if (!string.IsNullOrEmpty(root)) return root.Replace('\\', '/') + "/Generated/ItemIcons";
                }
            }
            catch (Exception) { }
            return "Assets/HorrorFPS/Generated/ItemIcons";
        }

        void RefreshList()
        {
            all.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            for (int i = 0; i < guids.Length; i++)
            {
                ItemData d = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (d != null) all.Add(d);
            }
            all.Sort((a, b) => string.Compare(a.DisplayNameBase, b.DisplayNameBase, StringComparison.OrdinalIgnoreCase));
        }

        void Select(ItemData d)
        {
            if (d == item) return;
            SaveNow();
            item = d;
            invRot = 0;
            if (itemEditor != null) { DestroyImmediate(itemEditor); itemEditor = null; }
            SessionState.SetString(SelKey, d != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(d)) : "");
            GUI.FocusControl(null);
            Repaint();
        }

        // ---------------------------------------------------------------- modelo e prévia
        GameObject SourcePrefab(ItemData d)
        {
            if (d == null) return null;
            if (d.iconShot == null) d.iconShot = new ItemIconShot();
            GameObject a = d.inspectPrefab, b = d.worldPrefab;
            return d.iconShot.modelSource == 0 ? (a != null ? a : b) : (b != null ? b : a);
        }

        void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.ambientColor = new Color(0.32f, 0.32f, 0.34f, 1f);
        }

        void DestroyModel()
        {
            if (instance != null) DestroyImmediate(instance);
            instance = null;
            loadedPrefab = null;
            points.Clear();
        }

        void LoadModel(GameObject prefab)
        {
            if (prefab == loadedPrefab && (prefab == null || instance != null)) return;
            DestroyModel();
            loadedPrefab = prefab;
            if (prefab == null) return;
            EnsurePreview();

            instance = Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = Vector3.zero;
            preview.AddSingleGO(instance);

            // só visual: scripts do prefab não rodam no estúdio
            Behaviour[] bs = instance.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < bs.Length; i++) if (bs[i] != null && !(bs[i] is Light)) bs[i].enabled = false;

            CollectPoints();
        }

        void CollectPoints()
        {
            points.Clear();
            if (instance == null) return;

            Renderer[] rs = instance.GetComponentsInChildren<Renderer>(false);
            int total = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                MeshFilter mf = rs[i].GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) total += mf.sharedMesh.vertexCount;
            }
            int stride = Mathf.Max(1, total / 20000);

            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh != null && mesh.isReadable && !(r is SkinnedMeshRenderer))
                {
                    Vector3[] v = mesh.vertices;
                    Matrix4x4 m = r.transform.localToWorldMatrix;
                    for (int k = 0; k < v.Length; k += stride) points.Add(m.MultiplyPoint3x4(v[k]));
                }
                else if (mesh != null && !(r is SkinnedMeshRenderer))
                {
                    AddBoxCorners(mesh.bounds, r.transform.localToWorldMatrix);
                }
                else
                {
                    Bounds b = r.bounds;     // alinhada ao mundo: serve para skinned, partículas etc.
                    AddBoxCorners(new Bounds(b.center, b.size), Matrix4x4.identity);
                }
            }

            if (points.Count == 0) { center = Vector3.zero; radius = 0.5f; points.Add(Vector3.one * 0.5f); points.Add(-Vector3.one * 0.5f); }

            Vector3 min = points[0], max = points[0];
            for (int i = 1; i < points.Count; i++) { min = Vector3.Min(min, points[i]); max = Vector3.Max(max, points[i]); }
            center = (min + max) * 0.5f;
            radius = 0.0001f;
            for (int i = 0; i < points.Count; i++) radius = Mathf.Max(radius, (points[i] - center).magnitude);
        }

        void AddBoxCorners(Bounds b, Matrix4x4 m)
        {
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                points.Add(m.MultiplyPoint3x4(p));
            }
        }

        /// <summary>Posiciona a câmera e as luzes para o enquadramento 's' numa imagem com a proporção 'aspect' (largura/altura).</summary>
        void SetupCamera(Camera cam, float aspect, ItemIconShot s, Color background)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = s.orthographic;
            cam.fieldOfView = Mathf.Clamp(s.fov, 5f, 90f);
            cam.aspect = aspect;

            Quaternion rot = Quaternion.Euler(s.pitch, s.yaw + 180f, s.roll);
            Quaternion inv = Quaternion.Inverse(rot);
            float k = Mathf.Clamp(1f - 2f * s.padding, 0.2f, 1f);
            float zoom = Mathf.Max(0.05f, s.zoom);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;

            float dist = 0f, ortho = 0.0001f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = inv * (points[i] - center);          // z > 0 = mais longe da câmera
                float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                if (s.orthographic)
                {
                    ortho = Mathf.Max(ortho, ay / k, ax / (aspect * k));
                }
                else
                {
                    dist = Mathf.Max(dist, ax / (tanH * k) - p.z, ay / (tanV * k) - p.z);
                }
            }

            float halfH;
            if (s.orthographic)
            {
                halfH = ortho / zoom;
                dist = radius * 3f;
                cam.orthographicSize = halfH;
            }
            else
            {
                dist = Mathf.Max(dist, radius * 0.2f) / zoom;
                halfH = dist * tanV;
            }

            Vector3 pan = rot * Vector3.right * (s.pan.x * halfH) + rot * Vector3.up * (s.pan.y * halfH);
            cam.transform.rotation = rot;
            cam.transform.position = center + pan - rot * Vector3.forward * dist;
            cam.nearClipPlane = Mathf.Max(0.005f, dist - radius * 1.5f);
            cam.farClipPlane = dist + radius * 3f + 1f;
            if (!s.orthographic) cam.nearClipPlane = Mathf.Max(0.005f, Mathf.Min(cam.nearClipPlane, dist * 0.5f));

            // luzes presas à câmera: o modelo fica com a mesma iluminação em qualquer ângulo
            if (preview.lights != null && preview.lights.Length > 0)
            {
                preview.lights[0].transform.rotation = rot * Quaternion.Euler(s.lightPitch, s.lightYaw, 0f);
                preview.lights[0].intensity = 1.15f;
                preview.lights[0].color = Color.white;
            }
            if (preview.lights != null && preview.lights.Length > 1)
            {
                preview.lights[1].transform.rotation = rot * Quaternion.Euler(-s.lightPitch * 0.5f, -s.lightYaw, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.lights[1].color = new Color(0.85f, 0.9f, 1f, 1f);
            }
        }

        // ---------------------------------------------------------------- foto
        static Vector2Int FrameCells(ItemData d)
        {
            return d.shape != null ? d.shape.GetSize(0) : new Vector2Int(1, 1);
        }

        static Vector2Int PhotoPixels(ItemData d, ItemIconShot s)
        {
            Vector2Int c = FrameCells(d);
            int ppc = Mathf.Clamp(s.pixelsPerCell, 16, 512);
            int w = c.x * ppc, h = c.y * ppc;
            float over = Mathf.Max(w, h) / (float)MaxPixels;
            if (over > 1f) { w = Mathf.Max(8, Mathf.RoundToInt(w / over)); h = Mathf.Max(8, Mathf.RoundToInt(h / over)); }
            return new Vector2Int(w, h);
        }

        /// <summary>Renderiza o modelo atual duas vezes (fundo preto e branco) e calcula a transparência pela diferença: bordas suaves e sem halo.</summary>
        Texture2D Render(ItemData d, ItemIconShot s)
        {
            EnsurePreview();
            Vector2Int px = PhotoPixels(d, s);
            int W = px.x, H = px.y;

            RenderTexture rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            RenderTexture prevActive = RenderTexture.active;
            Camera cam = preview.camera;
            Color32[] black, white;
            Texture2D readTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                SetupCamera(cam, (float)W / H, s, Color.black);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
                black = readTex.GetPixels32();

                cam.backgroundColor = Color.white;
                cam.Render();
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
                white = readTex.GetPixels32();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }

            Color32[] outp = new Color32[black.Length];
            for (int i = 0; i < outp.Length; i++)
            {
                Color32 b = black[i], w = white[i];
                float diff = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / (3f * 255f);
                float a = Mathf.Clamp01(1f - diff);
                if (a < 0.004f) { outp[i] = new Color32(0, 0, 0, 0); continue; }
                float inv = 1f / a;
                outp[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.r * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.g * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.b * inv), 0, 255),
                    (byte)Mathf.RoundToInt(a * 255f));
            }
            readTex.SetPixels32(outp);
            readTex.Apply(false);
            return readTex;
        }

        static string SafeFileName(string id)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            string s = id;
            for (int i = 0; i < bad.Length; i++) s = s.Replace(bad[i], '_');
            return string.IsNullOrEmpty(s) ? "item" : s;
        }

        /// <summary>Tira a foto do item com o enquadramento guardado nele, salva o PNG e liga o Sprite ao campo Icon. Devolve false se não deu.</summary>
        bool TakePhoto(ItemData d, bool quiet)
        {
            if (d == null) return false;
            if (d.iconShot == null) d.iconShot = new ItemIconShot();
            GameObject prefab = SourcePrefab(d);
            if (prefab == null)
            {
                if (!quiet) EditorUtility.DisplayDialog("Item Studio", "Este item não tem modelo 3D (Inspect Prefab ou World Prefab).", "OK");
                return false;
            }

            LoadModel(prefab);
            if (instance == null) return false;

            Texture2D tex = null;
            try
            {
                tex = Render(d, d.iconShot);
                byte[] png = tex.EncodeToPNG();

                WizardUtil.EnsureFolder(iconsFolder);
                string assetPath = iconsFolder + "/" + SafeFileName(d.Id) + ".png";
                string root = Directory.GetParent(Application.dataPath).FullName;
                File.WriteAllBytes(Path.Combine(root, assetPath), png);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti != null)
                {
                    bool changed = ti.textureType != TextureImporterType.Sprite
                                   || ti.spriteImportMode != SpriteImportMode.Single
                                   || !ti.alphaIsTransparency || ti.mipmapEnabled
                                   || ti.textureCompression != TextureImporterCompression.Uncompressed
                                   || ti.maxTextureSize < MaxPixels;
                    if (changed)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.alphaIsTransparency = true;
                        ti.mipmapEnabled = false;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
                        ti.filterMode = FilterMode.Bilinear;
                        ti.wrapMode = TextureWrapMode.Clamp;
                        ti.maxTextureSize = 2048;
                        ti.spritePixelsPerUnit = Mathf.Max(16, d.iconShot.pixelsPerCell);
                        ti.SaveAndReimport();
                    }
                }

                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sp == null)
                {
                    if (!quiet) EditorUtility.DisplayDialog("Item Studio", "O PNG foi salvo em " + assetPath + " mas não virou Sprite. Veja o Import Settings dele.", "OK");
                    return false;
                }
                Undo.RecordObject(d, "Item icon photo");
                d.icon = sp;
                EditorUtility.SetDirty(d);
                shotDirty = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[ItemStudio] Falha ao tirar a foto de " + d.Id + ": " + ex);
                if (!quiet) EditorUtility.DisplayDialog("Item Studio", "Não foi possível tirar a foto:\n" + ex.Message, "OK");
                return false;
            }
            finally
            {
                if (tex != null) DestroyImmediate(tex);
            }
        }

        void BatchMissing()
        {
            List<ItemData> todo = new List<ItemData>();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].icon == null && SourcePrefab(all[i]) != null) todo.Add(all[i]);
            if (todo.Count == 0)
            {
                EditorUtility.DisplayDialog("Item Studio", "Todos os itens que têm modelo 3D já têm ícone.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Item Studio",
                    todo.Count + " item(ns) sem ícone e com modelo 3D.\nVou fotografar cada um com o enquadramento padrão (ou o que já estiver guardado nele). Depois você pode refinar o ângulo de cada um.",
                    "Gerar", "Cancelar")) return;

            int ok = 0;
            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Item Studio", todo[i].DisplayNameBase, (float)i / todo.Count)) break;
                    if (TakePhoto(todo[i], true)) ok++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
            shotDirty = false;
            Debug.Log("[ItemStudio] " + ok + " ícone(s) gerado(s) em " + iconsFolder);
            Repaint();
        }

        // ---------------------------------------------------------------- novo item
        void NewItem()
        {
            WizardUtil.EnsureFolder(WizardUtil.ItemsFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(WizardUtil.ItemsFolder + "/NewItem.asset");
            ItemData d = CreateInstance<ItemData>();
            d.name = Path.GetFileNameWithoutExtension(path);
            d.id = d.name.ToLowerInvariant();
            d.displayName = new ItemTextSet { text = "Novo item" };
            AssetDatabase.CreateAsset(d, path);
            AssetDatabase.SaveAssets();
            RefreshList();
            Select(d);
            tab = 1;
            SessionState.SetInt(TabKey, tab);
            EditorGUIUtility.PingObject(d);
        }

        // ---------------------------------------------------------------- interface
        void OnGUI()
        {
            DrawToolbar();

            Rect left = new Rect(0f, TopH, LeftW, position.height - TopH);
            Rect right = new Rect(position.width - RightW, TopH, RightW, position.height - TopH);
            Rect middle = new Rect(LeftW, TopH, Mathf.Max(100f, position.width - LeftW - RightW), position.height - TopH);

            EditorGUI.DrawRect(new Rect(left.xMax - 1f, left.y, 1f, left.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(right.x, right.y, 1f, right.height), new Color(0f, 0f, 0f, 0.35f));

            DrawList(left);
            if (item == null)
            {
                GUI.Label(middle, "Escolha ou crie um item na lista.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            if (item.iconShot == null) item.iconShot = new ItemIconShot();

            DrawStage(middle);
            DrawRightPanel(right);
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Item Studio", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Gerar ícones que faltam", "Fotografa todos os itens com modelo 3D e sem ícone."), EditorStyles.toolbarButton, GUILayout.Width(160)))
                    BatchMissing();
                if (GUILayout.Button("＋ Novo item", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    NewItem();
            }
        }

        // ---------------------------------------------------------------- lista de itens
        void DrawList(Rect area)
        {
            GUILayout.BeginArea(area);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            string q = (search ?? "").Trim().ToLowerInvariant();
            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                ItemData d = all[i];
                if (d == null) continue;
                string nm = d.DisplayNameBase;
                if (q.Length > 0 && nm.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && d.Id.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                shown++;

                Rect r = GUILayoutUtility.GetRect(1f, 38f, GUILayout.ExpandWidth(true));
                bool sel = d == item;
                if (Event.current.type == EventType.Repaint)
                {
                    if (sel) EditorGUI.DrawRect(r, new Color(0.17f, 0.36f, 0.58f, 0.9f));
                    else if (r.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.06f));
                }
                Rect thumb = new Rect(r.x + 3f, r.y + 3f, 32f, 32f);
                EditorGUI.DrawRect(thumb, new Color(0f, 0f, 0f, 0.35f));
                if (d.icon != null) DrawSprite(thumb, d.icon);
                else GUI.Label(thumb, "—", EditorStyles.centeredGreyMiniLabel);

                GUI.Label(new Rect(r.x + 40f, r.y + 3f, r.width - 44f, 18f), nm, sel ? EditorStyles.whiteLabel : EditorStyles.label);
                string sub = d.icon == null ? (SourcePrefab(d) != null ? "sem ícone — pronto p/ foto" : "sem ícone e sem modelo") : d.Id;
                GUI.Label(new Rect(r.x + 40f, r.y + 19f, r.width - 44f, 16f), sub, EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    Select(d);
                    Event.current.Use();
                }
            }
            if (shown == 0) EditorGUILayout.HelpBox(all.Count == 0 ? "Nenhum item no projeto. Use \"＋ Novo item\" ou Tools > Horror FPS > 5. Create Sample Items." : "Nada encontrado.", MessageType.None);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void DrawSprite(Rect r, Sprite s)
        {
            if (s == null || s.texture == null) return;
            Rect tr = s.textureRect;
            Texture2D t = s.texture;
            Rect uv = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
            float aspect = tr.width / Mathf.Max(1f, tr.height);
            Rect fit = r;
            if (aspect > r.width / r.height) { fit.height = r.width / aspect; fit.y += (r.height - fit.height) * 0.5f; }
            else { fit.width = r.height * aspect; fit.x += (r.width - fit.width) * 0.5f; }
            GUI.DrawTextureWithTexCoords(fit, t, uv, true);
        }

        // ---------------------------------------------------------------- palco 3D
        void DrawStage(Rect area)
        {
            Rect stage = new Rect(area.x, area.y, area.width, area.height - BarH);
            Rect bar = new Rect(area.x, stage.yMax, area.width, BarH);
            ItemIconShot s = item.iconShot;

            GameObject prefab = SourcePrefab(item);
            LoadModel(prefab);

            EditorGUI.DrawRect(stage, new Color(0.1f, 0.1f, 0.11f, 1f));

            Vector2Int cells = FrameCells(item);
            Rect frame = FitFrame(stage, (float)cells.x / cells.y, 16f);

            if (prefab == null || instance == null)
            {
                GUI.Label(stage, "Este item não tem modelo 3D.\nArraste um prefab para cá (vira o Inspect Prefab/World Prefab do item).", EditorStyles.centeredGreyMiniLabel);
            }
            else if (Event.current.type == EventType.Repaint)
            {
                EnsurePreview();
                preview.BeginPreview(frame, GUIStyle.none);
                SetupCamera(preview.camera, frame.width / Mathf.Max(1f, frame.height), s, BgColors[bg]);
                preview.camera.Render();
                Texture t = preview.EndPreview();
                GUI.DrawTexture(frame, t, ScaleMode.StretchToFill, false);
            }

            if (Event.current.type == EventType.Repaint)
            {
                DrawFrameOverlay(frame, cells);
                Vector2Int px = PhotoPixels(item, s);
                GUI.Label(new Rect(stage.x + 8f, stage.y + 4f, stage.width - 16f, 18f),
                    "Moldura da foto: " + cells.x + "×" + cells.y + " células  →  " + px.x + "×" + px.y + " px", EditorStyles.miniLabel);
            }

            HandleStageInput(stage, frame, s);
            DrawBar(bar);
        }

        static Rect FitFrame(Rect stage, float aspect, float margin)
        {
            float availW = Mathf.Max(10f, stage.width - margin * 2f);
            float availH = Mathf.Max(10f, stage.height - margin * 2f - 14f);
            float w = availW, h = w / aspect;
            if (h > availH) { h = availH; w = h * aspect; }
            return new Rect(stage.x + (stage.width - w) * 0.5f, stage.y + 16f + (stage.height - 16f - h) * 0.5f, w, h);
        }

        void DrawFrameOverlay(Rect frame, Vector2Int cells)
        {
            Color line = new Color(1f, 1f, 1f, 0.9f);
            // células da forma que ficam vazias (item em L, etc.) escurecidas
            if (showGrid && item.shape != null)
            {
                float cw = frame.width / cells.x, ch = frame.height / cells.y;
                for (int y = 0; y < cells.y; y++)
                    for (int x = 0; x < cells.x; x++)
                        if (!item.shape.Contains(0, x, y))
                            EditorGUI.DrawRect(new Rect(frame.x + x * cw, frame.y + y * ch, cw, ch), new Color(0f, 0f, 0f, 0.55f));
                Color g = new Color(1f, 1f, 1f, 0.18f);
                for (int x = 1; x < cells.x; x++) EditorGUI.DrawRect(new Rect(frame.x + x * cw - 0.5f, frame.y, 1f, frame.height), g);
                for (int y = 1; y < cells.y; y++) EditorGUI.DrawRect(new Rect(frame.x, frame.y + y * ch - 0.5f, frame.width, 1f), g);
            }
            EditorGUI.DrawRect(new Rect(frame.x - 1f, frame.y - 1f, frame.width + 2f, 1f), line);
            EditorGUI.DrawRect(new Rect(frame.x - 1f, frame.yMax, frame.width + 2f, 1f), line);
            EditorGUI.DrawRect(new Rect(frame.x - 1f, frame.y, 1f, frame.height), line);
            EditorGUI.DrawRect(new Rect(frame.xMax, frame.y, 1f, frame.height), line);
        }

        void HandleStageInput(Rect stage, Rect frame, ItemIconShot s)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (stage.Contains(e.mousePosition) && e.button <= 2)
                    {
                        GUIUtility.hotControl = id;
                        dragButton = e.button;
                        dragAlt = e.alt;
                        if (e.clickCount == 2) { s.zoom = 1f; s.pan = Vector2.zero; MarkShot(); }
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (dragButton == 1 || dragButton == 2 || dragAlt)
                        {
                            float unit = Mathf.Max(1f, frame.height * 0.5f);
                            s.pan.x -= e.delta.x / unit;
                            s.pan.y += e.delta.y / unit;
                        }
                        else
                        {
                            s.yaw = Wrap(s.yaw - e.delta.x * 0.5f);
                            s.pitch = Mathf.Clamp(s.pitch + e.delta.y * 0.5f, -89f, 89f);
                        }
                        Repaint();
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        MarkShot();
                        e.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (stage.Contains(e.mousePosition))
                    {
                        s.zoom = Mathf.Clamp(s.zoom * (1f - e.delta.y * 0.04f), 0.2f, 6f);
                        MarkShot();
                        Repaint();
                        e.Use();
                    }
                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (stage.Contains(e.mousePosition) && HasPrefabDrag())
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if (e.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            AssignDraggedPrefab();
                        }
                        e.Use();
                    }
                    break;
            }
        }

        static float Wrap(float a)
        {
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }

        static bool HasPrefabDrag()
        {
            UnityEngine.Object[] refs = DragAndDrop.objectReferences;
            for (int i = 0; i < refs.Length; i++) if (refs[i] is GameObject) return true;
            return false;
        }

        void AssignDraggedPrefab()
        {
            UnityEngine.Object[] refs = DragAndDrop.objectReferences;
            for (int i = 0; i < refs.Length; i++)
            {
                GameObject go = refs[i] as GameObject;
                if (go == null) continue;
                Undo.RecordObject(item, "Item model");
                if (item.iconShot.modelSource == 1) item.worldPrefab = go; else item.inspectPrefab = go;
                EditorUtility.SetDirty(item);
                shotDirty = true;
                DestroyModel();
                Repaint();
                return;
            }
        }

        void MarkShot()
        {
            if (item == null) return;
            EditorUtility.SetDirty(item);
            shotDirty = true;
        }

        // ---------------------------------------------------------------- barra de baixo do palco
        void DrawBar(Rect bar)
        {
            ItemIconShot s = item.iconShot;
            GUILayout.BeginArea(new Rect(bar.x + 6f, bar.y + 6f, bar.width - 12f, bar.height - 8f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (Preset("Frente", 0f, 0f, s)) { }
                if (Preset("Trás", 180f, 0f, s)) { }
                if (Preset("Esquerda", -90f, 0f, s)) { }
                if (Preset("Direita", 90f, 0f, s)) { }
                if (Preset("Topo", 0f, 89f, s)) { }
                if (Preset("3/4", -35f, 22f, s)) { }
                if (GUILayout.Button(new GUIContent("Enquadrar", "Zoom 1× e centralizado (duplo clique no modelo)"), GUILayout.Width(74)))
                {
                    s.zoom = 1f; s.pan = Vector2.zero; MarkShot();
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(SourcePrefab(item) == null))
                {
                    Color oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                    bool shoot = GUILayout.Button(new GUIContent("📷  Tirar foto e usar como ícone", "Salva o PNG transparente, cria o Sprite e liga ao campo Icon do item."), GUILayout.Width(230), GUILayout.Height(26));
                    GUI.backgroundColor = oldBg;
                    if (shoot)
                    {
                        if (TakePhoto(item, false))
                        {
                            AssetDatabase.SaveAssets();
                            shotDirty = false;
                            ShowNotification(new GUIContent("Ícone de \"" + item.DisplayNameBase + "\" atualizado"));
                        }
                        GUIUtility.ExitGUI();
                    }
                }
            }
            GUILayout.EndArea();
        }

        bool Preset(string label, float yaw, float pitch, ItemIconShot s)
        {
            if (!GUILayout.Button(label, GUILayout.Width(label.Length > 4 ? 62 : 48))) return false;
            s.yaw = yaw; s.pitch = pitch; s.roll = 0f;
            MarkShot();
            Repaint();
            return true;
        }

        // ---------------------------------------------------------------- painel da direita
        void DrawRightPanel(Rect area)
        {
            GUILayout.BeginArea(area);
            int newTab = GUILayout.Toolbar(tab, new[] { "📷 Foto", "Item" }, GUILayout.Height(24));
            if (newTab != tab) { tab = newTab; SessionState.SetInt(TabKey, tab); GUI.FocusControl(null); }

            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
            if (tab == 0) DrawPhotoTab(); else DrawItemTab();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawItemTab()
        {
            EditorGUILayout.Space(4);
            if (itemEditor == null || itemEditor.target != item) itemEditor = Editor.CreateEditor(item);
            if (itemEditor != null)
            {
                EditorGUI.BeginChangeCheck();
                itemEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck()) { shotDirty = true; }
            }
        }

        void DrawPhotoTab()
        {
            ItemIconShot s = item.iconShot;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField(item.DisplayNameBase, EditorStyles.boldLabel);

            // modelo
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Modelo", EditorStyles.miniBoldLabel);
            s.modelSource = EditorGUILayout.Popup("Usar", s.modelSource, SourceNames);
            using (EditorGUI.ChangeCheckScope cc = new EditorGUI.ChangeCheckScope())
            {
                if (s.modelSource == 0)
                {
                    GameObject g = (GameObject)EditorGUILayout.ObjectField("Inspect Prefab", item.inspectPrefab, typeof(GameObject), false);
                    if (cc.changed) { Undo.RecordObject(item, "Item model"); item.inspectPrefab = g; DestroyModel(); }
                    if (item.inspectPrefab == null && item.worldPrefab != null) EditorGUILayout.LabelField("   (vazio: usando o World Prefab)", EditorStyles.miniLabel);
                }
                else
                {
                    GameObject g = (GameObject)EditorGUILayout.ObjectField("World Prefab", item.worldPrefab, typeof(GameObject), false);
                    if (cc.changed) { Undo.RecordObject(item, "Item model"); item.worldPrefab = g; DestroyModel(); }
                    if (item.worldPrefab == null && item.inspectPrefab != null) EditorGUILayout.LabelField("   (vazio: usando o Inspect Prefab)", EditorStyles.miniLabel);
                }
            }

            // câmera
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Câmera", EditorStyles.miniBoldLabel);
            s.yaw = EditorGUILayout.Slider("Giro", s.yaw, -180f, 180f);
            s.pitch = EditorGUILayout.Slider("Inclinação", s.pitch, -89f, 89f);
            s.roll = EditorGUILayout.Slider("Rolagem", s.roll, -180f, 180f);
            s.zoom = EditorGUILayout.Slider("Zoom", s.zoom, 0.2f, 6f);
            s.orthographic = EditorGUILayout.Toggle(new GUIContent("Ortográfica", "Sem perspectiva: bom para ícones 'chapados'."), s.orthographic);
            using (new EditorGUI.DisabledScope(s.orthographic))
                s.fov = EditorGUILayout.Slider("Campo de visão", s.fov, 8f, 70f);
            s.padding = EditorGUILayout.Slider(new GUIContent("Margem", "Folga entre o modelo e a borda da imagem."), s.padding, 0f, 0.3f);

            // luz
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Luz e fundo", EditorStyles.miniBoldLabel);
            s.lightYaw = EditorGUILayout.Slider("Luz: lado", s.lightYaw, -180f, 180f);
            s.lightPitch = EditorGUILayout.Slider("Luz: altura", s.lightPitch, -80f, 80f);
            int nbg = EditorGUILayout.Popup(new GUIContent("Fundo da prévia", "Só para visualizar; a foto sempre sai transparente."), bg, BgNames);
            if (nbg != bg) { bg = nbg; EditorPrefs.SetInt(BgKey, bg); }
            showGrid = EditorGUILayout.Toggle(new GUIContent("Mostrar células", "Grade das células e partes vazias da forma sobre a moldura."), showGrid);

            // saída
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Saída", EditorStyles.miniBoldLabel);
            int idx = Mathf.Max(0, Array.IndexOf(PixelOptions, s.pixelsPerCell));
            int nidx = EditorGUILayout.Popup(new GUIContent("Resolução por célula"), idx, PixelNames);
            s.pixelsPerCell = PixelOptions[nidx];
            Vector2Int px = PhotoPixels(item, s);
            EditorGUILayout.LabelField("Imagem final", px.x + " × " + px.y + " px (PNG transparente)");

            using (new EditorGUILayout.HorizontalScope())
            {
                iconsFolder = EditorGUILayout.TextField("Pasta dos ícones", iconsFolder);
                if (GUILayout.Button("…", GUILayout.Width(26)))
                {
                    string abs = EditorUtility.OpenFolderPanel("Pasta dos ícones", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        abs = abs.Replace('\\', '/');
                        string data = Application.dataPath.Replace('\\', '/');
                        if (abs.StartsWith(data, StringComparison.Ordinal)) { iconsFolder = "Assets" + abs.Substring(data.Length); GUI.FocusControl(null); }
                        else EditorUtility.DisplayDialog("Item Studio", "Escolha uma pasta dentro de Assets.", "OK");
                    }
                }
            }
            if (GUI.changed) EditorPrefs.SetString(FolderKey, iconsFolder);

            if (EditorGUI.EndChangeCheck())
            {
                MarkShot();
                Repaint();
            }

            // ícone atual + como fica no inventário
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Ícone atual", EditorStyles.miniBoldLabel);
            DrawInventoryPreview();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(item.icon == null))
                {
                    if (GUILayout.Button("Mostrar no Project")) EditorGUIUtility.PingObject(item.icon);
                    if (GUILayout.Button("Remover ícone"))
                    {
                        Undo.RecordObject(item, "Remove item icon");
                        item.icon = null;
                        EditorUtility.SetDirty(item);
                        shotDirty = true;
                    }
                }
            }
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox("O ícone gira junto com o item quando você rotaciona a peça no inventário. Se quiser uma imagem sua em vez da foto, é só arrastar um Sprite para o campo Icon na aba \"Item\".", MessageType.None);
        }

        /// <summary>Desenha a peça como aparece na grade (células escurecidas + ícone), com botão para girar.</summary>
        void DrawInventoryPreview()
        {
            const float cell = 44f;
            Vector2Int maxSize = item.shape.GetSize(0);
            float boxW = Mathf.Max(maxSize.x, maxSize.y) * cell + 16f;
            float boxH = boxW;
            Rect box = GUILayoutUtility.GetRect(1f, Mathf.Min(boxH, 240f), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(box, new Color(0.08f, 0.08f, 0.09f, 1f));
                float scale = Mathf.Min(1f, (box.height - 12f) / (Mathf.Max(maxSize.x, maxSize.y) * cell));
                float c = cell * scale;
                int rot = ((invRot % 4) + 4) % 4;
                Vector2Int size = item.shape.GetSize(rot);
                Vector2 origin = new Vector2(box.x + (box.width - size.x * c) * 0.5f, box.y + (box.height - size.y * c) * 0.5f);

                Color baseColor = item.icon != null ? Color.Lerp(item.color, Color.black, 0.45f) : item.color;
                Vector2Int[] cells = item.shape.GetCells(rot);
                for (int i = 0; i < cells.Length; i++)
                {
                    Rect cr = new Rect(origin.x + cells[i].x * c, origin.y + cells[i].y * c, c, c);
                    EditorGUI.DrawRect(cr, new Color(0f, 0f, 0f, 0.85f));
                    EditorGUI.DrawRect(new Rect(cr.x + 1f, cr.y + 1f, cr.width - 2f, cr.height - 2f), baseColor);
                }
                if (item.icon != null)
                {
                    Vector2Int s0 = item.shape.GetSize(0);
                    Vector2 mid = new Vector2(origin.x + size.x * c * 0.5f, origin.y + size.y * c * 0.5f);
                    Rect ir = new Rect(mid.x - s0.x * c * 0.5f, mid.y - s0.y * c * 0.5f, s0.x * c, s0.y * c);
                    Matrix4x4 m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(90f * rot, mid);
                    DrawSprite(ir, item.icon);
                    GUI.matrix = m;
                }
                else
                {
                    GUI.Label(box, "sem ícone", EditorStyles.centeredGreyMiniLabel);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↻ Girar peça", GUILayout.Width(100))) invRot = (invRot + 1) % 4;
                GUILayout.FlexibleSpace();
            }
        }
    }
}
