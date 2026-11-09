using System;
using UnityEditor;

namespace HorrorFPS
{
    /// <summary>
    /// Onde os assistentes do Editor (Tools > Horror FPS) gravam os assets que criam. A pasta-raiz do framework é
    /// descoberta pelo lugar onde ESTE script está (Editor/ProjectPaths.cs fica na raiz do framework), então o assistente
    /// continua certo se você renomear a pasta (ex.: de "HorrorFPS" para "Huggable Horrors") em vez de criar uma segunda.
    /// Antes, uns assistentes gravavam em "Assets/HorrorFPS/..." e outros na pasta renomeada: sobrava uma pasta "fantasma"
    /// com os itens, diálogos e materiais gerados, fora do repositório do jogo.
    /// </summary>
    public static class ProjectPaths
    {
        /// <summary>Nome original da pasta do pacote (README). Só serve de reserva e para achar dados antigos.</summary>
        public const string LegacyRoot = "Assets/HorrorFPS";

        const string ThisScript = "/Editor/ProjectPaths.cs";

        static string root;

        /// <summary>Pasta-raiz do framework no projeto, ex.: "Assets/Huggable Horrors" (sem barra no fim).</summary>
        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(root)) root = Detect();
                return root;
            }
        }

        /// <summary>
        /// Pasta de saída de um assistente, relativa à raiz (ex.: "Generated/Dialogues"). Usa a pasta da raiz atual; se ela ainda
        /// não existe mas a antiga ("Assets/HorrorFPS/...") existe, continua usando a antiga para não espalhar os mesmos assets
        /// em dois lugares. Para consolidar, mova a pasta antiga pelo Project (os GUIDs e as referências são mantidos).
        /// </summary>
        public static string Folder(string relative)
        {
            string preferred = Root + "/" + relative;
            if (AssetDatabase.IsValidFolder(preferred)) return preferred;
            if (Root != LegacyRoot)
            {
                string legacy = LegacyRoot + "/" + relative;
                if (AssetDatabase.IsValidFolder(legacy)) return legacy;
            }
            return preferred;
        }

        static string Detect()
        {
            try
            {
                // 1) o próprio script
                string[] guids = AssetDatabase.FindAssets("ProjectPaths t:MonoScript");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.EndsWith(ThisScript, StringComparison.OrdinalIgnoreCase))
                        return path.Substring(0, path.Length - ThisScript.Length);
                }

                // 2) o asset de controles (fica em <raiz>/Input/)
                const string inputTail = "/Input/HorrorControls.inputactions";
                guids = AssetDatabase.FindAssets("HorrorControls t:InputActionAsset");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.EndsWith(inputTail, StringComparison.OrdinalIgnoreCase))
                        return path.Substring(0, path.Length - inputTail.Length);
                }
            }
            catch (Exception)
            {
                // o AssetDatabase pode não estar pronto (ex.: durante a importação): vale a reserva
            }
            return LegacyRoot;
        }
    }
}
