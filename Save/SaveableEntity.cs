using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorFPS
{
    /// <summary>
    /// Qualquer componente que guarda estado no save. Implemente em um MonoBehaviour que esteja no mesmo objeto de um SaveableEntity.
    /// CaptureState devolve um JSON (use JsonUtility.ToJson de uma classe [Serializable] sua); RestoreState recebe o mesmo JSON.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Nome único dentro do objeto (ex.: "door", "transform").</summary>
        string SaveKey { get; }
        string CaptureState();
        void RestoreState(string json);
    }

    /// <summary>
    /// Marca um objeto da cena como "salvável": o estado dos ISaveable que estiverem nele entra no save e é reaplicado quando
    /// a cena for carregada de novo (mesmo depois de trocar de nível). Se o objeto for destruído (item pego, porta quebrada),
    /// ele continua destruído quando você voltar.
    ///
    /// O id precisa ser único na cena e estável. O Editor gera um GUID sozinho; se estiver vazio, usa o caminho na hierarquia.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HorrorFPS/Save/Saveable Entity")]
    public class SaveableEntity : MonoBehaviour
    {
        [Tooltip("Identificador único e estável deste objeto na cena. Gerado automaticamente — não copie entre objetos.")]
        [SerializeField] string id = "";

        ISaveable[] saveables;
        bool markedDestroyed;

        /// <summary>Id usado no save. Se o campo estiver vazio, usa o caminho do objeto na hierarquia.</summary>
        public string Id { get { return string.IsNullOrEmpty(id) ? PathId() : id; } }

        /// <summary>Marcado por quem vai destruir o objeto (ex.: Pickup), para o save já contar como destruído.</summary>
        public bool IsMarkedDestroyed { get { return markedDestroyed; } }

        public void MarkDestroyed() { markedDestroyed = true; }

        public void AssignNewId() { id = Guid.NewGuid().ToString("N"); }

        string PathId()
        {
            StringBuilder sb = new StringBuilder();
            for (Transform t = transform; t != null; t = t.parent)
                sb.Insert(0, "/" + t.name + "[" + t.GetSiblingIndex() + "]");
            return "path:" + sb;
        }

        ISaveable[] Saveables
        {
            get
            {
                if (saveables == null) saveables = GetComponents<ISaveable>();
                return saveables;
            }
        }

        public EntityRecord Capture()
        {
            EntityRecord record = new EntityRecord();
            record.id = Id;
            ISaveable[] list = Saveables;
            for (int i = 0; i < list.Length; i++)
            {
                ComponentRecord c = new ComponentRecord();
                c.key = list[i].SaveKey;
                c.json = list[i].CaptureState();
                record.components.Add(c);
            }
            return record;
        }

        public void Restore(EntityRecord record)
        {
            ISaveable[] list = Saveables;
            for (int r = 0; r < record.components.Count; r++)
            {
                ComponentRecord c = record.components[r];
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].SaveKey != c.key) continue;
                    try { list[i].RestoreState(c.json); }
                    catch (Exception e) { Debug.LogException(e, this); }
                    break;
                }
            }
        }

        /// <summary>Todas as entidades de uma cena, inclusive as de objetos desativados.</summary>
        public static List<SaveableEntity> Collect(Scene scene)
        {
            List<SaveableEntity> result = new List<SaveableEntity>();
            if (!scene.IsValid() || !scene.isLoaded) return result;

            List<SaveableEntity> temp = new List<SaveableEntity>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].GetComponentsInChildren(true, temp);
                result.AddRange(temp);
            }
            return result;
        }

#if UNITY_EDITOR
        void Reset() { AssignNewId(); }

        // Gera o id e corrige duplicatas (copiar/colar objeto ou instanciar o mesmo prefab várias vezes na cena).
        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
            if (!string.IsNullOrEmpty(id) && !IsDuplicate()) return;
            AssignNewId();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        bool IsDuplicate()
        {
            SaveableEntity[] all = Resources.FindObjectsOfTypeAll<SaveableEntity>();
            for (int i = 0; i < all.Length; i++)
            {
                SaveableEntity other = all[i];
                if (other == this || other.id != id) continue;
                if (UnityEditor.EditorUtility.IsPersistent(other)) continue; // asset de prefab
                if (other.gameObject.scene == gameObject.scene) return true;
            }
            return false;
        }
#endif
    }
}
