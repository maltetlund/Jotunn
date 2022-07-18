﻿using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Jotunn.Managers
{
    /// <summary>
    ///     Manager for handling undo and redo actions in mods. Can handle multiple undo queues.<br/>
    ///     Mods can make their own UndoActions using the provided <see cref="IUndoAction">interface</see>
    ///     or use the default ones Jötunn provides in <see cref="Jotunn.Utils.UndoActions"/>.<br />
    ///     Undo queues get automatically reset on every login and logout.
    /// </summary>
    public class UndoManager : IManager
    {
        /// <summary>
        ///     Interface for actions which can be added to the undo queue.
        /// </summary>
        public interface IUndoAction
        {
            /// <summary>
            ///     Code to revert whatever was executed.
            /// </summary>
            void Undo();

            /// <summary>
            ///     Code to replay whatever was executed.
            /// </summary>
            void Redo();

            /// <summary>
            ///     Message being displayed after a successful undo.
            /// </summary>
            string UndoMessage();

            /// <summary>
            ///     Message being displayed after a successful redo.
            /// </summary>
            string RedoMessage();
        }

        private static UndoManager _instance;

        /// <summary>
        ///     The singleton instance of this manager.
        /// </summary>
        public static UndoManager Instance => _instance ??= new UndoManager();

        /// <summary>
        ///     Hide .ctor
        /// </summary>
        private UndoManager() { }

        /// <summary>
        ///     Container to hold all live Overlays.
        /// </summary>
        private readonly Dictionary<string, UndoQueue> Queues = new Dictionary<string, UndoQueue>();

        /// <summary>
        ///     Registers all hooks.
        /// </summary>
        public void Init()
        {
            Main.Harmony.PatchAll(typeof(Patches));
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake)), HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void ClearUndoQueuesBefore(ZNetScene __instance) => Instance.Queues.Clear();

            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void ClearUndoQueuesAfter(ZNetScene __instance) => Instance.Queues.Clear();
        }
        
        /// <summary>
        ///     Add a message to the console or in the player HUD
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        /// <param name="priority"></param>
        private static void AddMessage(Terminal context, string message, bool priority = true)
        {
            if (context == Console.instance)
            {
                context.AddString(message);
            }
            var hud = MessageHud.instance;
            if (!hud)
            {
                return;
            }
            if (priority)
            {
                var items = hud.m_msgQeue.ToArray();
                hud.m_msgQeue.Clear();
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
                foreach (var item in items)
                {
                    hud.m_msgQeue.Enqueue(item);
                }
                hud.m_msgQueueTimer = 10f;
            }
            else
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
            }
        }

        /// <summary>
        ///     Add a new action to a queue. If a queue with the provided name does not exist it gets automatically created.
        /// </summary>
        /// <param name="name">Name of the queue. Multiple mods can use the same queue name.</param>
        /// <param name="action">Mod provided action which can undo and redo whatever was executed.</param>
        public void Add(string name, IUndoAction action)
        {
            if (Queues.TryGetValue(name, out var queue))
            {
                queue.Add(action);
                return;
            }

            queue = new UndoQueue();
            queue.Add(action);
            Queues.Add(name, queue);
        }

        public bool Undo(string name, Terminal terminal)
        {
            return Queues.TryGetValue(name, out var queue) && queue.Undo(terminal);
        }

        public bool Redo(string name, Terminal terminal)
        {
            return Queues.TryGetValue(name, out var queue) && queue.Redo(terminal);
        }

        private class UndoQueue
        {
            private List<IUndoAction> History = new List<IUndoAction>();
            private int Index = -1;
            private bool Executing = false;
            private int MaxSteps = 50;
            
            public void Add(IUndoAction action)
            {
                // During undo/redo more steps won't be added.
                if (Executing)
                {
                    return;
                }
                if (History.Count > MaxSteps - 1)
                {
                    History = History.Skip(History.Count - MaxSteps + 1).ToList();
                }
                if (Index < History.Count - 1)
                {
                    History = History.Take(Index + 1).ToList();
                }
                History.Add(action);
                Index = History.Count - 1;
            }

            public bool Undo(Terminal terminal)
            {
                if (Index < 0)
                {
                    AddMessage(terminal, "Nothing to undo.");
                    return false;
                }
                Executing = true;
                try
                {
                    History[Index].Undo();
                    AddMessage(terminal, History[Index].UndoMessage());
                }
                catch { }
                Index--;
                Executing = false;
                return true;
            }

            public bool Redo(Terminal terminal)
            {
                if (Index < History.Count - 1)
                {
                    Executing = true;
                    Index++;
                    try
                    {
                        History[Index].Redo();
                        AddMessage(terminal, History[Index].RedoMessage());
                    }
                    catch { }
                    Executing = false;
                    return true;
                }
                AddMessage(terminal, "Nothing to redo.");
                return false;
            }
        }
    }
}
