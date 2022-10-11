// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
//
// Copyright (c) 2019-present, Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Developer Agreement, located
// here: https://auth.magicleap.com/terms/developer
//
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%

using System;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.XR.MagicLeap;

namespace MagicLeap
{
    /// <summary>
    /// This class handles visualization of the video and the UI with the status
    /// of the recording.
    /// </summary>
    public class RawVideoCaptureVisualizer : MonoBehaviour
    {
        [SerializeField]
        private WebClient _webClient;

        private bool _ready;

        private int _compressionLevel = 1;

        /// <summary>
        /// Check for all required variables to be initialized.
        /// </summary>
        void Start()
        {
            _ready = false;
            Debug.Log("Initializing Raw Video Capture...");
            if (_webClient == null) {
                _webClient = GameObject.Find("WebClient").GetComponent<WebClient>();
            }
        }

        /// <summary>
        /// Handles video capture being started.
        /// </summary>
        public void OnCaptureStarted()
        {
            Debug.Log("Capture Starts...");
            
            if (_webClient == null) {
                Debug.LogError("WebClient Not Attached!");
            }
            _webClient.StartClient();
            _ready = true;
        }

        /// <summary>
        /// Handles video capture ending.
        /// </summary>
        public void OnCaptureEnded()
        {
            Debug.Log("Capture Ends...");
            _webClient.Close();
        }

        #if PLATFORM_LUMIN
        /// <summary>
        /// Display the raw video frame on the texture object.
        /// </summary>
        /// <param name="extras">Unused.</param>
        /// <param name="frameData">Contains raw frame bytes to manipulate.</param>
        /// <param name="frameMetadata">Unused.</param>
        public void OnRawCaptureDataReceived(MLCamera.ResultExtras extras, MLCamera.YUVFrameInfo frameData, MLCamera.FrameMetadata frameMetadata)
        {   // createYTexture(frameData);
            // createUVTexture(frameData);
            if (_ready) {
                sendFrame(frameData);
            }
        }
        #endif

        /// <summary>
        /// Disables the rendere.
        /// </summary>
        public void OnRawCaptureEnded()
        {
            Debug.Log("Ended capture data");
        }

        private async void sendFrame(MLCamera.YUVFrameInfo frameData) {
            await Task.Run(() => {
                YUV2RGB(frameData);
            });
        }

        private void YUV2RGB(MLCamera.YUVFrameInfo frameData) {
            
            // Debug.Log(String.Format("Y | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.Y.Width, frameData.Y.Height, frameData.Y.Stride, frameData.Y.Data.Length));
            // Debug.Log(String.Format("U | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.U.Width, frameData.U.Height, frameData.U.Stride, frameData.U.Data.Length));
            // Debug.Log(String.Format("V | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.V.Width, frameData.V.Height, frameData.V.Stride, frameData.V.Data.Length));
            _ready = false;
            int bufferSize = (frameData.Y.Data.Length + frameData.U.Data.Length + frameData.V.Data.Length) / this._compressionLevel;
            byte[] newBuffer = new byte[bufferSize];
            if (this._compressionLevel == 1) {
                Array.Copy(frameData.Y.Data, 0, newBuffer, 0, frameData.Y.Data.Length);
                Array.Copy(frameData.U.Data, 0, newBuffer, frameData.Y.Data.Length, frameData.U.Data.Length);
                Array.Copy(frameData.V.Data, 0, newBuffer, frameData.Y.Data.Length + frameData.U.Data.Length, frameData.V.Data.Length);
            } else {
                int i = 0;
                for (int j=0; j < frameData.Y.Data.Length; j++) {
                    if ((j % this._compressionLevel) == 0) {
                        newBuffer[i++] = frameData.Y.Data[j];
                    }
                }

                for (int j=0; j < frameData.U.Data.Length; j++) {
                    if ((j % this._compressionLevel) == 0) {
                        newBuffer[i++] = frameData.U.Data[j];
                    }
                }

                for (int j=0; j < frameData.V.Data.Length; j++) {
                    if ((j % this._compressionLevel) == 0) {
                        newBuffer[i++] = frameData.V.Data[j];
                    }
                }
            }
            _webClient.Send(newBuffer);
            _ready = true;
        }
    }
}
