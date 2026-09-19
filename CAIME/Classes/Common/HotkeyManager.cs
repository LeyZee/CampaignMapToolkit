using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CAIME
{
    public enum ProcessHotkeyResult
    {
        Handled,
        Passed,
        Failed
    }

    public interface IHotkeyHandler
    {
        ProcessHotkeyResult ProcessHotkeyCombination(Key[] keys);
    }

    public static class HotkeyManager
    {
        private static List<IHotkeyHandler> subscribers;
        private static HashSet<Key> pressedKeys;

        static HotkeyManager()
        {
            subscribers = new List<IHotkeyHandler>();
            pressedKeys = new HashSet<Key>();
        }

        public static void Subscribe(IHotkeyHandler subscriber)
        {
            if (!subscribers.Contains(subscriber))
            {
                subscribers.Add(subscriber);
            }
        }

        public static void UnSubscribe(IHotkeyHandler subscriber)
        {
            if (subscribers.Contains(subscriber))
            {
                subscribers.Remove(subscriber);
            }
        }

        public static void ProcessKeyDown(object sender, KeyEventArgs e)
        {
            ClearKeys();

            if (e.Key != Key.System)
            {
                pressedKeys.Add(e.Key);
            }

            // trigger hotkey for subscribers if LeftCtrl + another key is pressed
            if (pressedKeys.Count > 0)
            {
                foreach (var subscriber in subscribers)
                {
                    var result = subscriber.ProcessHotkeyCombination(pressedKeys.ToArray());
                    if (result == ProcessHotkeyResult.Handled)
                    {
                        ClearKeys();
                        break;
                    }
                    else
                    if (result == ProcessHotkeyResult.Failed)
                    {
                        // TODO: Do any additional fail handling here
                        ClearKeys();
                        break;
                    }
                }
            }
        }

        public static void ProcessKeyUp(object sender, KeyEventArgs e)
        {
            if (pressedKeys.Contains(e.Key))
            {
                pressedKeys.Remove(e.Key);
            }
        }

        private static void ClearKeys()
        {
            foreach (var key in pressedKeys.ToArray())
            {
                if (Keyboard.IsKeyDown(key))
                {
                    continue;
                }

                pressedKeys.Remove(key);
            }
        }
    }
}
