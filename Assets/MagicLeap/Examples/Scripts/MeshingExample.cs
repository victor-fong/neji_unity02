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

#if UNITY_EDITOR || PLATFORM_LUMIN

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;
using UnityEngine.XR;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Compression;
using MagicLeap.Core.StarterKit;
using Neji;

namespace MagicLeap
{
    /// <summary>
    /// This represents all the runtime control over meshing component in order to best visualize the
    /// affect changing parameters has over the meshing API.
    /// </summary>
    public class MeshingExample : MonoBehaviour
    {
        private static readonly int PROCESS_THREAD_NUM = 4;

        [SerializeField]
        private WebClient _webClient;

        [SerializeField, Tooltip("The spatial mapper from which to update mesh params.")]
        private MLSpatialMapper _mlSpatialMapper = null;

        [SerializeField, Tooltip("Visualizer for the meshing results.")]
        private MeshingVisualizer _meshingVisualizer = null;

        [SerializeField, Space, Tooltip("A visual representation of the meshing bounds.")]
        private GameObject _visualBounds = null;

        [SerializeField, Space, Tooltip("Flag specifying if mesh extents are bounded.")]
        private bool _bounded = false;

        [SerializeField, Space, Tooltip("The text to place mesh data on.")]
        private Text _statusLabel = null;

        [SerializeField, Space, Tooltip("Prefab to shoot into the scene.")]
        private GameObject _shootingPrefab = null;

        [SerializeField, Space, Tooltip("MLControllerConnectionHandlerBehavior reference.")]
        private MLControllerConnectionHandlerBehavior _controllerConnectionHandler = null;

        private MeshingVisualizer.RenderMode _renderMode = MeshingVisualizer.RenderMode.Wireframe;
        private int _renderModeCount;

        private static readonly Vector3 _boundedExtentsSize = new Vector3(2.0f, 2.0f, 2.0f);
        private static readonly Vector3 _boundlessExtentsSize = new Vector3(10.0f, 10.0f, 10.0f);

        private const float SHOOTING_FORCE = 300.0f;
        private const float MIN_BALL_SIZE = 0.2f;
        private const float MAX_BALL_SIZE = 0.5f;
        private const int BALL_LIFE_TIME = 10;

        private Camera _camera = null;
        
        private MyQueue<MeshId> processQueue;
        private MyQueue<byte[]> sendQueue;

        private List<Thread> processThreads;

        private Thread sendThread;

        /// <summary>
        /// Initializes component data and starts MLInput.
        /// </summary>
        void Awake()
        {
            if (_mlSpatialMapper == null)
            {
                Debug.LogError("Error: MeshingExample._mlSpatialMapper is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_meshingVisualizer == null)
            {
                Debug.LogError("Error: MeshingExample._meshingVisualizer is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_visualBounds == null)
            {
                Debug.LogError("Error: MeshingExample._visualBounds is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_statusLabel == null)
            {
                Debug.LogError("Error: MeshingExample._statusLabel is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_shootingPrefab == null)
            {
                Debug.LogError("Error: MeshingExample._shootingPrefab is not set, disabling script.");
                enabled = false;
                return;
            }
            if (_controllerConnectionHandler == null)
            {
                Debug.LogError("Error MeshingExample._controllerConnectionHandler not set, disabling script.");
                enabled = false;
                return;
            }

            _renderModeCount = System.Enum.GetNames(typeof(MeshingVisualizer.RenderMode)).Length;

            _camera = Camera.main;

            #if PLATFORM_LUMIN
            MLInput.OnControllerButtonDown += OnButtonDown;
            MLInput.OnTriggerDown += OnTriggerDown;
            MLInput.OnControllerTouchpadGestureStart += OnTouchpadGestureStart;
            #endif
        }

        /// <summary>
        /// Set correct render mode for meshing and update meshing settings.
        /// </summary>
        void Start()
        {
            #if PLATFORM_LUMIN
            // Assure that if the 'WorldReconstruction' privilege is missing, then it is logged for all users.
            MLResult result = MLPrivilegesStarterKit.Start();
            if (result.IsOk)
            {
                result = MLPrivilegesStarterKit.CheckPrivilege(MLPrivileges.Id.WorldReconstruction);
                if (result.Result != MLResult.Code.PrivilegeGranted)
                {
                    Debug.LogErrorFormat("Error: MeshingExample failed to create Mesh Subsystem due to missing 'WorldReconstruction' privilege. Please add to manifest. Disabling script.");
                    enabled = false;
                    return;
                }
                MLPrivilegesStarterKit.Stop();
            }
            else
            {
                Debug.LogErrorFormat("Error: MeshingExample failed starting MLPrivileges, disabling script. Reason: {0}", result);
                enabled = false;
                return;
            }

            result = MLHeadTracking.Start();
            if (result.IsOk)
            {
                MLHeadTracking.RegisterOnHeadTrackingMapEvent(OnHeadTrackingMapEvent);
            }
            else
            {
                Debug.LogError("MeshingExample could not register to head tracking events because MLHeadTracking could not be started.");
            }
            #endif

            if (_webClient == null) {
                _webClient = GameObject.Find("WebClient").GetComponent<WebClient>();
            }
            _meshingVisualizer.SetRenderers(_renderMode);

            _mlSpatialMapper.gameObject.transform.position = _camera.gameObject.transform.position;
            _mlSpatialMapper.gameObject.transform.localScale = _bounded ? _boundedExtentsSize : _boundlessExtentsSize;

            processQueue = new MyQueue<MeshId>(100);
            sendQueue = new MyQueue<byte[]>(100);

            processThreads = new List<Thread>(PROCESS_THREAD_NUM);
            sendThread = new Thread(()=>SendFromQueue(sendQueue));
            for (int i = 0; i<PROCESS_THREAD_NUM; i++) {
                Thread processThread = new Thread(()=>serializeMesh());
                processThreads.Add(processThread);
            }

            _mlSpatialMapper.meshAdded += onMeshAdded;
            _mlSpatialMapper.meshUpdated += onMeshUpdated;
            _mlSpatialMapper.meshRemoved += onMeshRemoved;

            _webClient.StartClient();
            foreach (Thread processThread in processThreads) {
                processThread.Start();
            }
            sendThread.Start();

            _visualBounds.SetActive(_bounded);
        }

        private void SendFromQueue(MyQueue<byte[]> sendQueue) {
            Debug.Log("Send Thread running...");
            try{
                while (true){
                    byte[] data = sendQueue.Dequeue();
                    _webClient.Send(data);
                }
            } catch (Exception e){
                UnityEngine.Debug.LogError(e);
            }
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

        private byte[] appendArrays(params byte[][] arrays){
            int newLength = 0;
            foreach (byte[] array in arrays){
                newLength += array.Length;
            }
            byte[] result = new byte[newLength];
            int i = 0;
            foreach (byte[] array in arrays){
                Buffer.BlockCopy(array, 0, result, i, array.Length);
                i += array.Length;
            }

            return result;
        }

        private void serializeMesh(){
            Debug.Log("Thread Running...");
            try{
                while (true) {
                    MeshId meshId = processQueue.Dequeue();
                    // Debug.Log(String.Format("Processing {0}", meshId));
                    byte[] meshIdBytes = Encoding.ASCII.GetBytes(meshId.ToString());
                    GameObject go = _mlSpatialMapper.meshIdToGameObjectMap[meshId];
                    Mesh mesh = go.GetComponent<MeshFilter>().mesh;
                    Vector3[] vertices = mesh.vertices;

                    byte[] idLength = BitConverter.GetBytes(meshIdBytes.Length);
                    byte[] verticesNum = BitConverter.GetBytes(vertices.Length);

                    byte[] verticesBuffer = serializeVectors(vertices);
                    byte[] buffer = appendArrays(idLength, meshIdBytes, verticesNum, verticesBuffer);
                    
                    buffer = _deflate(buffer);
                    sendQueue.Enqueue(buffer);
                }
            } catch(Exception e){
                UnityEngine.Debug.LogError(e);
            }
        }

        private byte[] serializeVectors(Vector3[] vectors){
            byte[] result = new byte[vectors.Length * 3 * 4];
            int i = 0;
            foreach (Vector3 vector in vectors){
                byte[] xBytes = BitConverter.GetBytes(vector.x);
                Buffer.BlockCopy(xBytes, 0, result, i, xBytes.Length);
                i += 4;

                byte[] yBytes = BitConverter.GetBytes(vector.y);
                Buffer.BlockCopy(yBytes, 0, result, i, yBytes.Length);
                i += 4;

                byte[] zBytes = BitConverter.GetBytes(vector.z);
                Buffer.BlockCopy(zBytes, 0, result, i, zBytes.Length);
                i += 4;
            }
            return result;
        }

        public void onMeshAdded(MeshId meshId) {
            processQueue.Enqueue(meshId);
        }

        public void onMeshUpdated(MeshId meshId) {
            processQueue.Enqueue(meshId);
        }

        public void onMeshRemoved(MeshId meshId) {
            processQueue.Enqueue(meshId);
        }

        /// <summary>
        /// Update mesh polling center position to camera.
        /// </summary>
        void Update()
        {
            _mlSpatialMapper.gameObject.transform.position = _camera.gameObject.transform.position;

            UpdateStatusText();
        }

        /// <summary>
        /// Cleans up the component.
        /// </summary>
        void OnDestroy()
        {
            #if PLATFORM_LUMIN
            MLInput.OnControllerTouchpadGestureStart -= OnTouchpadGestureStart;
            MLInput.OnTriggerDown -= OnTriggerDown;
            MLInput.OnControllerButtonDown -= OnButtonDown;
            MLHeadTracking.UnregisterOnHeadTrackingMapEvent(OnHeadTrackingMapEvent);
            MLHeadTracking.Stop();
            #endif
        }

        /// <summary>
        /// Updates examples status text.
        /// </summary>
        private void UpdateStatusText()
        {
            _statusLabel.text = string.Format("<color=#dbfb76><b>{0} {1}</b></color>\n{2}: {3}\n",
                LocalizeManager.GetString("Controller"),
                LocalizeManager.GetString("Data"),
                LocalizeManager.GetString("Status"),
                LocalizeManager.GetString(ControllerStatus.Text));

            _statusLabel.text += string.Format(
                "\n<color=#dbfb76><b>{0} {1}</b></color>\n{2} {3}: {4}\n{5} {6}: {7}\n{8}: {9}",
                LocalizeManager.GetString("Meshing"),
                LocalizeManager.GetString("Data"),
                LocalizeManager.GetString("Render"),
                LocalizeManager.GetString("Mode"),
                LocalizeManager.GetString(_renderMode.ToString()),
                LocalizeManager.GetString("Bounded"),
                LocalizeManager.GetString("Extents"),
                LocalizeManager.GetString(_bounded.ToString()),
                LocalizeManager.GetString("LOD"),
                #if UNITY_2019_3_OR_NEWER
                LocalizeManager.GetString(MLSpatialMapper.DensityToLevelOfDetail(_mlSpatialMapper.density).ToString())
                #else
                LocalizeManager.GetString(_mlSpatialMapper.levelOfDetail.ToString())
                #endif
                );
        }

        /// <summary>
        /// Handles the event for button down. Changes render mode if bumper is pressed or
        /// changes from bounded to boundless and viceversa if home button is pressed.
        /// </summary>
        /// <param name="controllerId">The id of the controller.</param>
        /// <param name="button">The button that is being released.</param>
        private void OnButtonDown(byte controllerId, MLInput.Controller.Button button)
        {
            if (_controllerConnectionHandler.IsControllerValid(controllerId))
            {
                if (button == MLInput.Controller.Button.Bumper)
                {
                    _renderMode = (MeshingVisualizer.RenderMode)((int)(_renderMode + 1) % _renderModeCount);
                    _meshingVisualizer.SetRenderers(_renderMode);
                }
                else if (button == MLInput.Controller.Button.HomeTap)
                {
                    _bounded = !_bounded;

                    _visualBounds.SetActive(_bounded);
                    _mlSpatialMapper.gameObject.transform.localScale = _bounded ? _boundedExtentsSize : _boundlessExtentsSize;
                }
            }
        }

        /// <summary>
        /// Handles the event for trigger down. Throws a ball in the direction of
        /// the camera's forward vector.
        /// </summary>
        /// <param name="controllerId">The id of the controller.</param>
        /// <param name="button">The button that is being released.</param>
        private void OnTriggerDown(byte controllerId, float value)
        {
            if (_controllerConnectionHandler.IsControllerValid(controllerId))
            {
                // TODO: Use pool object instead of instantiating new object on each trigger down.
                // Create the ball and necessary components and shoot it along raycast.
                GameObject ball = Instantiate(_shootingPrefab);

                ball.SetActive(true);
                float ballsize = UnityEngine.Random.Range(MIN_BALL_SIZE, MAX_BALL_SIZE);
                ball.transform.localScale = new Vector3(ballsize, ballsize, ballsize);
                ball.transform.position = _camera.gameObject.transform.position;

                Rigidbody rigidBody = ball.GetComponent<Rigidbody>();
                if (rigidBody == null)
                {
                    rigidBody = ball.AddComponent<Rigidbody>();
                }
                rigidBody.AddForce(_camera.gameObject.transform.forward * SHOOTING_FORCE);

                Destroy(ball, BALL_LIFE_TIME);
            }
        }

        /// <summary>
        /// Handles the event for touchpad gesture start. Changes level of detail
        /// if gesture is swipe up.
        /// </summary>
        /// <param name="controllerId">The id of the controller.</param>
        /// <param name="gesture">The gesture getting started.</param>
        private void OnTouchpadGestureStart(byte controllerId, MLInput.Controller.TouchpadGesture gesture)
        {
            #if PLATFORM_LUMIN
            if (_controllerConnectionHandler.IsControllerValid(controllerId) &&
                gesture.Type == MLInput.Controller.TouchpadGesture.GestureType.Swipe && gesture.Direction == MLInput.Controller.TouchpadGesture.GestureDirection.Up)
            {
                #if UNITY_2019_3_OR_NEWER
                _mlSpatialMapper.density = MLSpatialMapper.LevelOfDetailToDensity((MLSpatialMapper.DensityToLevelOfDetail(_mlSpatialMapper.density) == MLSpatialMapper.LevelOfDetail.Maximum) ? MLSpatialMapper.LevelOfDetail.Minimum : (MLSpatialMapper.DensityToLevelOfDetail(_mlSpatialMapper.density) + 1));
                #else
                _mlSpatialMapper.levelOfDetail = ((_mlSpatialMapper.levelOfDetail == MLSpatialMapper.LevelOfDetail.Maximum) ? MLSpatialMapper.LevelOfDetail.Minimum : (_mlSpatialMapper.levelOfDetail + 1));
                #endif
            }
            #endif
        }

        /// <summary>
        /// Handle in charge of refreshing all meshes if a new session occurs
        /// </summary>
        /// <param name="mapEvents"> Map Events that happened. </param>
        private void OnHeadTrackingMapEvent(MLHeadTracking.MapEvents mapEvents)
        {
            #if PLATFORM_LUMIN
            if (mapEvents.IsNewSession())
            {
                _mlSpatialMapper.DestroyAllMeshes();
                _mlSpatialMapper.RefreshAllMeshes();
            }
            #endif
        }
    }
}

#endif
