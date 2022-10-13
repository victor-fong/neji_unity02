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
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.XR.MagicLeap;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.Compression;
using Neji;

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
        private static readonly int PROCESS_THREAD_NUM = 10;
        private static readonly int COMPRESSION_LEVEL = 3;

        private MyQueue<MLCamera.YUVFrameInfo> processQueue;
        private MyQueue<byte[]> sendQueue;
        private List<Thread> processThreads;
        private Thread sendThread;

        

        /// <summary>
        /// Check for all required variables to be initialized.
        /// </summary>
        void Start()
        {
            processQueue = new MyQueue<MLCamera.YUVFrameInfo>(1);
            sendQueue = new MyQueue<byte[]>(1);
            processThreads = new List<Thread>(PROCESS_THREAD_NUM);
            
            UnityEngine.Debug.Log("Initializing Raw Video Capture...");
            if (_webClient == null) {
                _webClient = GameObject.Find("WebClient").GetComponent<WebClient>();
            }

            for (int i = 0; i<PROCESS_THREAD_NUM; i++) {
                Thread processThread = new Thread(()=>YUV2RGB(processQueue, sendQueue));
                processThreads.Add(processThread);
            }
            sendThread = new Thread(()=>SendFromQueue(sendQueue));
        }

        private void SendFromQueue(MyQueue<byte[]> sendQueue) {
            try{
                while (true){
                    byte[] data = sendQueue.Dequeue();
                    _webClient.Send(data);
                }
            } catch (Exception e){
                UnityEngine.Debug.LogError(e);
            }
        }

        /// <summary>
        /// Handles video capture being started.
        /// </summary>
        public void OnCaptureStarted()
        {
            UnityEngine.Debug.Log("Capture Starts...");
            if (_webClient == null) {
                UnityEngine.Debug.LogError("WebClient Not Attached!");
            }
            _webClient.StartClient();
            foreach (Thread processThread in processThreads) {
                processThread.Start();
            }
            sendThread.Start();
        }

        /// <summary>
        /// Handles video capture ending.
        /// </summary>
        public void OnCaptureEnded()
        {
            UnityEngine.Debug.Log("Capture Ends...");
            
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
            
            // UnityEngine.Debug.Log(String.Format("Y | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.Y.Width, frameData.Y.Height, frameData.Y.Stride, frameData.Y.Data.Length));
            // UnityEngine.Debug.Log(String.Format("U | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.U.Width, frameData.U.Height, frameData.U.Stride, frameData.U.Data.Length));
            // UnityEngine.Debug.Log(String.Format("V | Width: {0} | Height: {1} | Stride {2} | Data: {3}", frameData.V.Width, frameData.V.Height, frameData.V.Stride, frameData.V.Data.Length));
            processQueue.Enqueue(frameData);

        }
        #endif

        /// <summary>
        /// Disables the rendere.
        /// </summary>
        public void OnRawCaptureEnded()
        {
            UnityEngine.Debug.Log("Ended capture data");
        }

        private static int addHeader(int headerValue, int index, byte[] buffer) {
            byte[] headerBytes = BitConverter.GetBytes(headerValue);    
            if (BitConverter.IsLittleEndian)
                Array.Reverse(headerBytes);
            Array.Copy(headerBytes, 0, buffer, index, headerBytes.Length);
            return headerBytes.Length;
        }

        private byte[] _deflate(byte[] data){
            MemoryStream output = new MemoryStream();
            using (GZipStream dstream = new GZipStream(output, System.IO.Compression.CompressionMode.Compress))
            {
                dstream.Write(data, 0, data.Length);
                dstream.Close();
            }
            return output.ToArray();
        }

        public void YUV2RGB(MyQueue<MLCamera.YUVFrameInfo> processQueue, MyQueue<byte[]> sendQueue) {
            try{
                while (true) {
                    MLCamera.YUVFrameInfo frameData = processQueue.Dequeue();
                    // Stopwatch stopwatch = new Stopwatch();
                    // stopwatch.Start();
                    // int bufferSize = (frameData.Y.Data.Length + frameData.U.Data.Length + frameData.V.Data.Length) / (COMPRESSION_LEVEL * COMPRESSION_LEVEL);
                    
                    int headerSize = 8;
                    int newWidth = ((int) frameData.Y.Width) / COMPRESSION_LEVEL;
                    int newHeight = ((int) frameData.Y.Height) / COMPRESSION_LEVEL;
                    int bufferSize = newWidth * newHeight / 4 * 6;


                    byte[] newBuffer = new byte[headerSize + bufferSize];

                    int i = 0;
                    i += addHeader(newWidth, i, newBuffer);
                    i += addHeader(newHeight, i, newBuffer);

                    if (COMPRESSION_LEVEL == 1) {
                        Array.Copy(frameData.Y.Data, 0, newBuffer, i, frameData.Y.Data.Length);
                        Array.Copy(frameData.U.Data, 0, newBuffer, i + frameData.Y.Data.Length, frameData.U.Data.Length);
                        Array.Copy(frameData.V.Data, 0, newBuffer, i + frameData.Y.Data.Length + frameData.U.Data.Length, frameData.V.Data.Length);
                    } else {
                        
                        for (int j=0; j < frameData.Y.Data.Length; j++) {
                            if ((j % COMPRESSION_LEVEL) == 0 && ((j % frameData.Y.Stride) < frameData.Y.Width)) {
                                if (((j / frameData.Y.Stride) % COMPRESSION_LEVEL) == 0) {
                                    newBuffer[i++] = frameData.Y.Data[j];
                                }
                            }
                        }

                        for (int j=0; j < frameData.U.Data.Length; j++) {
                            if ((j % COMPRESSION_LEVEL) == 0 && ((j % frameData.U.Stride) < frameData.U.Width)) {
                                if (((j / frameData.U.Stride) % COMPRESSION_LEVEL) == 0) {
                                    newBuffer[i++] = frameData.U.Data[j];
                                }
                            }
                        }

                        for (int j=0; j < frameData.V.Data.Length; j++) {
                            if ((j % COMPRESSION_LEVEL) == 0 && ((j % frameData.V.Stride) < frameData.V.Width)) {
                                if (((j / frameData.V.Stride) % COMPRESSION_LEVEL) == 0) {
                                    newBuffer[i++] = frameData.V.Data[j];
                                }
                            }
                        }

                        // UnityEngine.Debug.LogError(String.Format("NEW HEIGHT {0} | NEW WIDTH {1} | NEW BUFFER SIZE {2} | INDEX AT {3}", newHeight, newWidth, newBuffer.Length, i));
                    }
                    // stopwatch.Stop();
                    // UnityEngine.Debug.LogError(String.Format("PreProcessing Time is {0} ms", stopwatch.ElapsedMilliseconds));
                    newBuffer = _deflate(newBuffer);
                    sendQueue.Enqueue(newBuffer);
                }
            } catch(Exception e){
                UnityEngine.Debug.LogError(e);
            }
        }
    }
}

