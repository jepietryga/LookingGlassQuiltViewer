﻿// SharpDX.DirectInput を利用してアプリがバックグラウンドでも Looking Glass のボタンを使えるようにする
//
// ButtonManage の代わりに使ってください
//
// Author; Kirurobo
// License: MIT License


// WindowsならばバックグラウンドでもLookingGlassのボタンが使えるようDirectInputを利用
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define USE_DIRECTINPUT
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

#if USE_DIRECTINPUT
using SharpDX.DirectInput;
#else
using UnityEngine.InputSystem;
#endif

namespace Kirurobo
{
    public class HoloPlayButtonListener
    {
        /// <summary>
        /// Looking Glass が備えるボタン
        /// </summary>
        public enum HoloPlayButton
        {
            Square = 0,
            Left = 1,
            Right = 2,
            Circle = 3,
        }

        /// <summary>
        /// 現在のボタン押下状態
        /// </summary>
        private Dictionary<HoloPlayButton, bool> currentState = new Dictionary<HoloPlayButton, bool>();

        /// <summary>
        /// 前フレームのボタン押下状態
        /// </summary>
        private Dictionary<HoloPlayButton, bool> lastState = new Dictionary<HoloPlayButton, bool>();

        public delegate void KeyEventHandler(HoloPlayButton button);
        public event KeyEventHandler OnkeyDown;
        public event KeyEventHandler OnkeyUp;

#if USE_DIRECTINPUT
        /// <summary>
        /// 発見されたデバイスが保存される
        /// </summary>
        private List<SharpDX.DirectInput.Joystick> holoplayDevices = new List<SharpDX.DirectInput.Joystick>();


        /// <summary>
        /// 指定ボタンが押されているか判定
        /// </summary>
        /// <param name="state"></param>
        /// <param name="button"></param>
        /// <returns></returns>
        private bool IsPressed(JoystickState state, HoloPlayButton button)
        {
            return (state.Buttons[(int)button]);
        }
#else
        /// <summary>
        /// Looking Glassの接続数
        /// </summary>
        private int holoplayDevicesCount = 0;

        /// <summary>
        /// KeyCode.Joystick?Button? と HoloPlayButton の対応リスト
        /// </summary>
        private Dictionary<KeyCode, HoloPlayButton> joyStickButtonMap = new Dictionary<KeyCode, HoloPlayButton>();
           
#endif

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public HoloPlayButtonListener()
        {
            // 最初にデバイスを取得
            RefreshDevices();
        }

        /// <summary>
        /// 取得されているデバイス数を返す
        /// </summary>
        /// <returns></returns>
        public int GetDeviceCount()
        {
#if USE_DIRECTINPUT
            return holoplayDevices.Count;
#else
            return holoplayDevicesCount;
#endif
        }

        /// <summary>
        /// 有効なデバイスを全て取得
        /// </summary>
        public void RefreshDevices()
        {
#if USE_DIRECTINPUT
            // 参考 http://csdegame.net/sharpdx/dx_input_pad.html

            DirectInput dinput = new DirectInput();

            // Looking Glass は Supplemental になっているようなのでそこを探す
            foreach (DeviceInstance device in dinput.GetDevices(SharpDX.DirectInput.DeviceType.Supplemental, DeviceEnumerationFlags.AllDevices))
            {
                if (device.ProductName.Contains("HoloPlay"))
                {
                    Joystick joystick = new Joystick(dinput, device.ProductGuid);
                    if (joystick != null)
                    {
                        holoplayDevices.Add(joystick);
                    }
                }
            }
#else
            // デバイス一覧を初期化
            holoplayDevicesCount = 0;
            joyStickButtonMap.Clear();

            var joystickNames = Input.GetJoystickNames();
            for (int i = 0; i < joystickNames.Length; i++)
            {
                // Looking Glass か判別
                if (joystickNames[i].ToLower().Contains("holoplay"))
                {
                    // 接続数をカウント
                    holoplayDevicesCount++;

                    // Unity上でのジョイスティック番号に合わせたKeyCodeとボタンの対応を記憶
                    foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton))) {
                        string buttonName = String.Format(
                            "Joystick{0}Button{1}",
                            i + 1,
                            (int)button
                            );
                        KeyCode code;
                        if (Enum.TryParse<KeyCode>(buttonName, out code))
                        {
                            joyStickButtonMap.Add(code, button);
                        }
                     }
                }
            }
#endif

            // ボタン状態を初期化
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                currentState[button] = false;
                lastState[button] = false;
            }
        }

        /// <summary>
        /// このメソッドを毎フレーム呼んでください
        /// </summary>
        public void Update()
        {
            UpdateButtonState();
            ProcessEvent();
        }

        /// <summary>
        /// 現在のボタン押下状態を調べる
        /// </summary>
        private void UpdateButtonState()
        {
#if USE_DIRECTINPUT
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                currentState[button] = false;
            }
            foreach (var device in holoplayDevices)
            {
                // キャプチャ開始
                device.Acquire();
                device.Poll();

                // データ取得
                var state = device.GetCurrentState();

                // 取得できなければ終了
                if (state == null)
                {
                    break;
                }

                foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
                {
                    // 複数デバイスがあれば、いずれかが押されたら押下と判断
                    if (IsPressed(state, button)) currentState[button] = true;
                }
            }
#else
            // 現在の状態をクリア
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                currentState[button] = false;
            }

            // 現在のボタン押下状態を取得
            foreach (var map in joyStickButtonMap)
            {
                if (Input.GetKey(map.Key))
                {
                    // 複数デバイスがあれば、いずれかが押されたら押下と判断
                    currentState[map.Value] = true;
                }
            }
#endif
        }

        /// <summary>
        /// 前回の状態と現在の状態を比較してイベント処理
        /// </summary>
        private void ProcessEvent()
        {
            // 各キーのイベントを処理
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                if (!lastState[button] && currentState[button])
                {
                    // Key down
                    OnkeyDown?.Invoke(button);

                }
                else if (lastState[button] && !currentState[button])
                {
                    // Key up
                    OnkeyUp?.Invoke(button);
                }
                lastState[button] = currentState[button];
            }
        }

        /// <summary>
        /// 現在ボタンが押されているか否かを返す
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        public bool GetKey(HoloPlayButton button)
        {
            return currentState[button];
        }
    }
}
