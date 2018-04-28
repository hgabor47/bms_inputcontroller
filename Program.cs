using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using MouseKeyboardActivityMonitor;
using MouseKeyboardActivityMonitor.WinApi;
using System.Runtime.InteropServices;
using System.IO;
using System.Net.Sockets;
using System.Xml;
using System.Threading;

namespace InputController
{
    class Program
    {
        
        static bool debug = true;
        static string shipUUID = "af675eb4-385e-4fda-a3a1-fdebd8901085";  //Input Controller UUID
        static BabylonMS.BabylonMS bms;
        static BabylonMS.BMSEventSessionParameter session=null;

        static MouseHooker hook;
        // static int sensitivity = 1; //1.best but slow, 10 is quite well

        public class Form1 : Form
        {
            public Form1() { }
        }


        [STAThread]
        static void Main(string[] args)
        {

            Application.EnableVisualStyles();

            bms = BabylonMS.BabylonMS.ShipDocking(shipUUID, args);
            bms.Connected += Connected;
            bms.Disconnected += Disconnected;
            bms.NewInputFrame += NewInputFrame;
            bms.Waitbytes += WaitBytes;
            bms.WaitBytesMS = 5;
            bms.OpenGate(false);//Client         
            //BabylonMS.Util.setPriorityUp();            
            
            Application.Run();
            //Application.Run(new Form1());

            while (true)
            {
                try
                {
                    Thread.Sleep(5);
                    for (int i = 0; i < 4; i++)
                    {
                        Application.DoEvents();
                    }
                }
                catch (Exception ) { }
            }
            
        }

        static void WaitBytes(BabylonMS.BMSEventSessionParameter psession)
        {            
            for (int i = 0; i < 1; i++)
            {
                Application.DoEvents();
            }
        }

        static void Connected(BabylonMS.BMSEventSessionParameter psession)
        {
            if (debug) Console.WriteLine("InputController Connected.");
            session = psession;
            hook = new MouseHooker();
        }
        static void Disconnected(BabylonMS.BMSEventSessionParameter session)
        {
            hook.destroy();
            hook = null;
        }
        static void NewInputFrame(BabylonMS.BMSEventSessionParameter session)
        {
            Console.WriteLine("NEW INPUT PACK");
            if (session.inputPack.FieldsCount() > 0)
            {
                BabylonMS.BMSPack outputpack = new BabylonMS.BMSPack();
                byte command = (byte)session.inputPack.GetField(0).getValue(0);
                switch (command)
                {
                    case VRMainContentExporter.VRCEShared.CONST_IC_MODE:
                        //outputpack.AddField("IDX", BabylonMS.BabylonMS.CONST_FT_INT8).Value((byte)buf.position_in_buffer);
                        //bms.TransferPacket(session.writer, outputpack, true);
                        break;
                }
            }
        }
        
        class MouseHooker
        {
            private MouseHookListener m_MouseHookManager;

            public static int left;
            public static int top;
            public string buttonState = "";
            MouseButtons mousebutton;
            bool VirtualMouse;
            int VirtualMouseX;
            int VirtualMouseY;
            int storeMouseX;  //in virtual mode start
            int storeMouseY;

            public MouseHooker()
            {
                buttonState = "";
                VirtualMouse = false;
                
                m_MouseHookManager = new MouseHookListener(new GlobalHooker());
                m_MouseHookManager.Enabled = true;
                //m_MouseHookManager.MouseMove += HookManager_MouseMove;
                //m_MouseHookManager.MouseDown += HookManager_MouseDown;
                m_MouseHookManager.MouseUp += HookManager_MouseUp;
                m_MouseHookManager.MouseDownExt += MouseDownExt;
                m_MouseHookManager.MouseMoveExt += MouseMoveExt;
            }

            public void destroy()
            {
                m_MouseHookManager.Enabled = false;
                //m_MouseHookManager.MouseMove -= HookManager_MouseMove;
                //m_MouseHookManager.MouseDown -= HookManager_MouseDown;
                m_MouseHookManager.MouseUp -= HookManager_MouseUp;
                m_MouseHookManager.MouseDownExt -= MouseDownExt;
                m_MouseHookManager.MouseMoveExt -= MouseMoveExt;
                m_MouseHookManager.Dispose();
                m_MouseHookManager = null;
            }

            int virtualOrigoX;
            int virtualOrigoY;
            public void startVirtualMousePosition(int x, int y)
            {
                User32.POINT p;
                storeMouseX = x;
                storeMouseY = y;
                VirtualMouseX = 0;
                VirtualMouseY = 0;
                User32.SetCursorPos(0, 0);
                User32.GetCursorPos(out p);
                virtualOrigoX = p.X;
                virtualOrigoY = p.Y;
            }
            public void restoreVirtualMousePosition()
            {
                User32.SetCursorPos(storeMouseX, storeMouseY);

            }


            //int cnt = 0;
            private void MouseDownExt(object sender, MouseEventExtArgs e)
            {
                

                BabylonMS.BMSPack outputpack = InitPack();
                if (e.Button == MouseButtons.XButton2)
                {
                    buttonState = "down";
                    AddToPack(outputpack,e);
                    buttonState = "up";
                    AddToPack(outputpack, e);
                    buttonState = "";
                    e.Handled = true;
                    
                }//mousedown elintézi

                else
                {
                    if (e.Button == MouseButtons.XButton1) //switch virtual mode on and off
                    {
                        MouseEventArgs e2;
                        if (!VirtualMouse)
                        {
                            VirtualMouse = true;
                            startVirtualMousePosition(e.X, e.Y);
                            e2 = new MouseEventArgs(e.Button, e.Clicks, VirtualMouseX, VirtualMouseY, e.Delta);
                        }
                        else
                        {
                            VirtualMouse = false;
                            restoreVirtualMousePosition();
                            e2 = new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta);
                        }
                        AddToPack(outputpack, e2);
                        buttonState = "up";
                        AddToPack(outputpack, e2);
                        buttonState = "";
                        e.Handled = true;
                    }
                    else
                    {
                        mousebutton = e.Button;
                        buttonState = "down";
                        AddToPack(outputpack, e);
                        if (VirtualMouse)
                        {
                            buttonState = "up";
                            lastButtonForVirtualUP = e.Button;
                        }
                    }
                }
                if (VirtualMouse)
                {
                    e.Handled = true;
                }
                if (bms != null)
                {
                    session.outputPack = outputpack;
                    Task t2 = Task.Factory.StartNew(() =>
                    {
                        session.TransferPacket(true);
                    });
                    //sessiontranferpacket(outputpack);
                    //bms.TransferPacket(session.writer, outputpack, true);
                }
            }

            private void MouseMoveExt(object sender, MouseEventExtArgs e)
            {
                BabylonMS.BMSPack outputpack = InitPack();
                if ((e.Button == MouseButtons.XButton1) || (e.Button == MouseButtons.XButton2))
                {
                    e.Handled = true;
                    return;
                }

                if (VirtualMouse)
                {
                    MouseEventArgs e2;

                    VirtualMouseX += e.X - virtualOrigoX;
                    VirtualMouseY += e.Y - virtualOrigoY;
                    User32.SetCursorPos(0, 0);
                    e2 = new MouseEventArgs(e.Button, e.Clicks, VirtualMouseX, VirtualMouseY, e.Delta);
                    AddToPack(outputpack, e2);
                    e.Handled = true;
                }
                else
                {
                    AddToPack(outputpack, e);
                }
                if (bms != null)
                {
                    sessiontransferpacket(outputpack); //lossy veszteséges a down és UP viszont nem
                    //bms.TransferPacket(session.writer, outputpack, true);
                }

            }

            private void sessiontransferpacket(BabylonMS.BMSPack outputpack)
            {
                if (session.writelock.WaitOne(0))  //kimarad ha nem tudom elküldeni
                {
                    session.writelock.Release();
                    session.outputPack = outputpack;
                    Task t2 = Task.Factory.StartNew(() =>
                    {
                        session.TransferPacket(true);
                    });
                }
            }
            #region outdated
            private void HookManager_MouseMove(object sender, MouseEventArgs e)
            {
                /*            MemoryStream mem;
                            if (!VirtualMouse)
                            {
                                mem = getXML(e);
                                if (media.mouseClient != null)
                                {
                                    NetworkStream stream = media.mouseClient.GetStream();
                                    sendPack(mem, stream);
                                }
                            }
                            */
            }

            MouseButtons lastButtonForVirtualUP; //for virtual up
            private void HookManager_MouseDown(object sender, MouseEventArgs e)
            {/*
            mousebutton=e.Button;
            buttonState = "down";
            var mem = getXML(e);
            if (media.mouseClient != null)
            {
                NetworkStream stream = media.mouseClient.GetStream();
                sendPack(mem,stream);
            }        
            if (VirtualMouse)
            {
                buttonState = "up";
                lastButtonForVirtualUP = e.Button;
            }
            */
            }
#endregion

            private void HookManager_MouseUp(object sender, MouseEventArgs e)
            {
                mousebutton = MouseButtons.None;
                buttonState = "up";
                BabylonMS.BMSPack outputpack = InitPack();
                AddToPack(outputpack, e);
                if (bms != null)
                {
                    session.outputPack = outputpack;
                    Task t2 = Task.Factory.StartNew(() =>
                    {
                        session.TransferPacket(true);
                    });

                    //sessiontranferpacket(outputpack);
                    //bms.TransferPacket(session.writer, outputpack, true);
                }
                buttonState = "";
            }

            /*
            private void sendPack(MemoryStream mem, NetworkStream stream)
            {
                long headersize = mem.Length;
                byte[] packetLength = BitConverter.GetBytes(headersize);
                stream.Write(packetLength, 0, 4);
                mem.WriteTo(stream);
                stream.Flush();
            }
            */

            public BabylonMS.BMSPack InitPack()
            {
                BabylonMS.BMSPack outputpack = new BabylonMS.BMSPack();
                outputpack.AddField("CMD", BabylonMS.BabylonMS.CONST_FT_INT8).Value(VRMainContentExporter.VRCEShared.CONST_IC_EVENT);
                BabylonMS.BMSField btn = outputpack.AddField("BUTTON", BabylonMS.BabylonMS.CONST_FT_INT32);
                BabylonMS.BMSField X = outputpack.AddField("X", BabylonMS.BabylonMS.CONST_FT_INT16);
                BabylonMS.BMSField Y = outputpack.AddField("Y", BabylonMS.BabylonMS.CONST_FT_INT16);
                return outputpack;
            }

            public void AddToPack(BabylonMS.BMSPack outputpack,MouseEventArgs e)
            {
                BabylonMS.BMSField btn = outputpack.GetFieldByName("BUTTON"); //Able to call more then one and add extend field array 
                BabylonMS.BMSField X = outputpack.GetFieldByName("X");
                BabylonMS.BMSField Y = outputpack.GetFieldByName("Y");
                uint button = (uint)e.Button;
                if (VirtualMouse)
                {
                    if ((e.Button != MouseButtons.None) && (buttonState.CompareTo("") == 0))
                    {
                        buttonState = "down";
                    }
                    button |= VRMainContentExporter.VRCEShared.CONST_MOUSEBUTTON_VIRTUAL;
                    if (buttonState.ToLower().CompareTo("down") == 0)
                    {
                        button |= VRMainContentExporter.VRCEShared.CONST_MOUSEBUTTON_DOWN;
                    }
                    btn.Value((int)button);
                    X.Value((UInt16)VirtualMouseX);
                    Y.Value((UInt16)VirtualMouseY);
                }
                else
                {
                    if (buttonState.ToLower().CompareTo("down") == 0)
                    {
                        button |= VRMainContentExporter.VRCEShared.CONST_MOUSEBUTTON_DOWN;
                    }
                    btn.Value((int)button);
                    X.Value((UInt16)(e.X - left));
                    Y.Value((UInt16)(e.Y - top));
                }
            }

            public static void setMousePos(int x, int y)
            {
                System.Windows.Forms.Cursor.Position = new Point(x, y);
            }

            public static void setMouseArea(int x, int y, int w, int h)
            {
                System.Windows.Forms.Cursor.Clip = new Rectangle(x, y, w, h);
                left = x;
                top = y;
            }


        }


    }

    public class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
    }
}
