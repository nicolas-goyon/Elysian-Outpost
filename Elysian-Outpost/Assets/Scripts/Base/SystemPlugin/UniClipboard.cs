// This file is a changed version of UniClipboard:
// https://github.com/sanukin39/UniClipboard
// The Elysian Outpost contributors changed it in 2025.
//
// Copyright (c) 2016 wataru.sanuki
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// SPDX-License-Identifier: MIT

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