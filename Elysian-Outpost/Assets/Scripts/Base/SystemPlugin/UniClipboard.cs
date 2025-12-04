using System;
using System.Reflection;
using UnityEngine;

namespace Base.SystemPlugin
{
    public static class UniClipboard
    {
        private static IBoard _board;

        private static IBoard Board
        {
            get
            {
                if (_board == null)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                _board = new AndroidBoard();
#elif UNITY_IOS && !UNITY_TVOS && !UNITY_EDITOR
                _board = new IOSBoard ();
#else
                    _board = new StandardBoard();
#endif
                }

                return _board;
            }
        }

        public static void SetText(string str)
        {
            Board.SetText(str);
        }

        public static string GetText()
        {
            return Board.GetText();
        }
    }

    internal interface IBoard
    {
        void SetText(string str);
        string GetText();
    }

    internal class StandardBoard : IBoard
    {
        private static PropertyInfo _mSystemCopyBufferProperty = null;

        private static PropertyInfo GetSystemCopyBufferProperty()
        {
            if (_mSystemCopyBufferProperty != null) return _mSystemCopyBufferProperty;

            Type T = typeof(GUIUtility);
            _mSystemCopyBufferProperty = T.GetProperty("systemCopyBuffer", BindingFlags.Static | BindingFlags.Public);

            if (_mSystemCopyBufferProperty == null)
            {
                _mSystemCopyBufferProperty =
                    T.GetProperty("systemCopyBuffer", BindingFlags.Static | BindingFlags.NonPublic);
            }

            if (_mSystemCopyBufferProperty == null)
            {
                throw new Exception(
                    "Can't access internal member 'GUIUtility.systemCopyBuffer' it may have been removed / renamed");
            }

            return _mSystemCopyBufferProperty;
        }

        public void SetText(string str)
        {
            PropertyInfo p = GetSystemCopyBufferProperty();
            p.SetValue(null, str, null);
        }

        public string GetText()
        {
            PropertyInfo p = GetSystemCopyBufferProperty();
            return (string)p.GetValue(null, null);
        }
    }

    // TODO : Test 
#if UNITY_IOS && !UNITY_TVOS
class IOSBoard : IBoard {
    [DllImport("__Internal")]
    static extern void SetText_ (string str);
    [DllImport("__Internal")]
    static extern string GetText_();

    public void SetText(string str){
        if (Application.platform != RuntimePlatform.OSXEditor) {
            SetText_ (str);
        }
    }

    public string GetText(){
        return GetText_();
    }
}
#endif

#if UNITY_ANDROID
class AndroidBoard : IBoard
{
    public void SetText(string str)
    {
        GetClipboardManager().Call("setText", str);
    }

    public string GetText()
    {
        return GetClipboardManager().Call<string>("getText");
    }

    AndroidJavaObject GetClipboardManager()
    {
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var staticContext = new AndroidJavaClass("android.content.Context");
        var service = staticContext.GetStatic<AndroidJavaObject>("CLIPBOARD_SERVICE");
        return activity.Call<AndroidJavaObject>("getSystemService", service);
    }
}
#endif
}