using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Diagnostics;

public class WebClient : MonoBehaviour
{
    
    private Socket _sender;

    void Start()
    {
        
    }

    void OnDestroy() {
        _sender.Shutdown(SocketShutdown.Both);
        _sender.Close();
    }

    public void Close() {
        _sender.Shutdown(SocketShutdown.Both);
        _sender.Close();
    }

    private void _send(byte[] data) {
        // Stopwatch stopwatch = new Stopwatch();
        // stopwatch.Start();
        byte[] lengthBytes = BitConverter.GetBytes(data.Length);
        // UnityEngine.Debug.Log(String.Format("Sending {0} bytes", data.Length));
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        _sender.Send(lengthBytes);

        int totalSent = 0; 
        int sent = -1;
        while((sent = _sender.Send(data, totalSent, data.Length - totalSent, SocketFlags.None)) > 0)
        {
            totalSent += sent;
        }
        // stopwatch.Stop();
        // UnityEngine.Debug.LogError(String.Format("Transmission Time is {0} ms", stopwatch.ElapsedMilliseconds));
    }


    public void Send(byte[] data){
        _send(data);
    }

    public void StartClient() {
        try
        {
            // Connect to a Remote server
            // Get Host IP Address that is used to establish a connection
            // In this case, we get one IP address of localhost that is IP : 127.0.0.1
            // If a host has multiple addresses, you will get a list of addresses

            // IPHostEntry host = Dns.GetHostEntry("localhost");
            // IPAddress ipAddress = host.AddressList[0];
            IPAddress ipAddress = IPAddress.Parse("192.168.86.42");
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 8989);

            // Create a TCP/IP  socket.
            _sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Connect the socket to the remote endpoint. Catch any errors.
            try
            {
                // Connect to Remote EndPoint
                _sender.Connect(remoteEP);
                UnityEngine.Debug.Log("WebClient Ready...");
            }
            catch (ArgumentNullException e)
            {
                UnityEngine.Debug.Log(e.StackTrace);
            }
            catch (SocketException e)
            {
                UnityEngine.Debug.Log(e.StackTrace);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.StackTrace);
            }

        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.StackTrace);
        }
    }

    // Update is called once per frame
    // void Update()
    // {
        
    // }
}
