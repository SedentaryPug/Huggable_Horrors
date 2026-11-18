using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Acha as classes de característica disponíveis (as de série e as que o time criar): as que herdam de StalkerTrait, são
    /// [Serializable], concretas e têm construtor sem parâmetros. Usado pelo menu "Adicionar característica" do Inspector e do canvas.
    /// </summary>
    public static class StalkerTraitTypes
    {
        static readonly HashSet<string> warned = new HashSet<string>();

        public static List<Type> Find()
        {
            List<Type> list = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom<StalkerTrait>())
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
                if (!t.IsSerializable)
                {
                    if (warned.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' herda de StalkerTrait mas não tem [Serializable]: ela não aparece no menu do perfil em grafo. Ponha [Serializable] em cima da classe.");
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    if (warned.Add(t.FullName)) Debug.LogWarning("[Stalker] A classe '" + t.Name + "' não tem construtor sem parâmetros: ela não aparece no menu do perfil em grafo.");
                    continue;
                }
                list.Add(t);
            }
            list.Sort(delegate (Type a, Type b) { return string.Compare(StalkerTraitInfo.Path(a), StalkerTraitInfo.Path(b), StringComparison.Ordinal); });
            return list;
        }
    }
}
