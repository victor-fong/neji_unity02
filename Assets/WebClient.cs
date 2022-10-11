using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

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

    private byte[] _deflate(byte[] data){
        MemoryStream output = new MemoryStream();
        using (GZipStream dstream = new GZipStream(output, System.IO.Compression.CompressionMode.Compress))
        {
            dstream.Write(data, 0, data.Length);
            dstream.Close();
        }
        return output.ToArray();
    }

    private void _send(byte[] data) {
        Debug.Log("Sending...");
        data = _deflate(data);
        byte[] lengthBytes = BitConverter.GetBytes(data.Length);
        Debug.Log(String.Format("Sending {0} bytes", data.Length));
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        _sender.Send(lengthBytes);

        int totalSent = 0; 
        int sent = -1;
        while((sent = _sender.Send(data, totalSent, data.Length - totalSent, SocketFlags.None)) > 0)
        {
            totalSent += sent;
        }
        Debug.Log("Finished sending...");
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
                Debug.Log("WebClient Ready...");
            }
            catch (ArgumentNullException e)
            {
                Debug.Log(e.StackTrace);
            }
            catch (SocketException e)
            {
                Debug.Log(e.StackTrace);
            }
            catch (Exception e)
            {
                Debug.Log(e.StackTrace);
            }

        }
        catch (Exception e)
        {
            Debug.Log(e.StackTrace);
        }
    }

    // Update is called once per frame
    // void Update()
    // {
        
    // }
}
