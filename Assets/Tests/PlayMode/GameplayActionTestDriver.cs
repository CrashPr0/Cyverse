using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Cyverse.Tests
{
    /// <summary>
    /// Reflection bridge required because Unity test assemblies cannot
    /// reference the predefined Assembly-CSharp assembly directly. Tests
    /// still send the exact GameplayAction objects used by live input.
    /// </summary>
    internal static class GameplayActionTestDriver
    {
        public static bool Interact(object target) => Apply(target, "Interact");
        public static bool Move(object target, Vector3 worldVelocity, float deltaSeconds) =>
            Apply(target, "Move", worldVelocity, deltaSeconds);
        public static bool Navigate(object target, int delta) => Apply(target, "Navigate", delta);
        public static bool Choose(object target, int option) => Apply(target, "Choose", option);
        public static bool Select(object target, int field, int option) =>
            Apply(target, "Select", field, option);
        public static bool Append(object target, string text) => Apply(target, "Append", text);
        public static bool ClearText(object target) => Apply(target, "ClearText");
        public static bool Submit(object target) => Apply(target, "Submit");
        public static bool Scrub(object target, float seconds) => Apply(target, "Scrub", seconds);
        public static bool Cancel(object target) => Apply(target, "Cancel");

        public static IEnumerator RunDeterministic(string methodName, object target)
        {
            Type adapter = FindType("Cyverse.Testing.DeterministicGameplayAdapter");
            MethodInfo method = adapter.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new MissingMethodException(adapter.FullName, methodName);
            return (IEnumerator)method.Invoke(null, new[] { target, null });
        }

        public static bool RunDeterministicAction(string methodName, object target)
        {
            Type adapter = FindType("Cyverse.Testing.DeterministicGameplayAdapter");
            MethodInfo method = adapter.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new MissingMethodException(adapter.FullName, methodName);
            return (bool)method.Invoke(null, new[] { target, null });
        }

        private static bool Apply(object target, string factory, params object[] factoryArguments)
        {
            Type actionType = FindType("Cyverse.Interaction.GameplayAction");
            Type dispatcherType = FindType("Cyverse.Interaction.GameplayActions");
            MethodInfo create = actionType.GetMethod(factory, BindingFlags.Public | BindingFlags.Static);
            MethodInfo apply = dispatcherType.GetMethod("TryApply", BindingFlags.Public | BindingFlags.Static);
            if (create == null) throw new MissingMethodException(actionType.FullName, factory);
            if (apply == null) throw new MissingMethodException(dispatcherType.FullName, "TryApply");
            object action = create.Invoke(null, factoryArguments);
            return (bool)apply.Invoke(null, new[] { target, action, null });
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            throw new TypeLoadException("Type not found: " + fullName);
        }
    }
}
