/*
 * 
 *  Some cursor specific things
 *  https://stackoverflow.com/questions/10541014/hiding-system-cursor
 *  
 */


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
        public static bool TEST_INPUTCONTROLLER = false;
        static bool debug = true;
        static string shipUUID = "af675eb4-385e-4fda-a3a1-fdebd8901085";  //Input Controller UUID
        static BabylonMS.BabylonMS bms;
        static BabylonMS.BMSEventSessionParameter session=null;
        static Point origo = new Point(-10000,-10000);

        static MouseHooker hook;
        // static int sensitivity = 1; //1.best but slow, 10 is quite well

        static bool exitSystem = false;
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;
        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        private static bool Handler(CtrlType sig)
        {
            Console.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");
            try
            {
                User32.ShowMouse();
            }
            catch (Exception) { }
            Console.WriteLine("Cleanup complete");
            //allow main to run off
            exitSystem = true;
            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);
            return true;
        }


        public class Form1 : Form
        {
            public Form1() { }
        }


        [STAThread]
        static void Main(string[] args)
        {
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            Application.EnableVisualStyles();
            User32.HideMouse();
            User32.ShowMouse();

            bms = BabylonMS.BabylonMS.ShipDocking(shipUUID, args);
            bms.Connected += Connected;
            bms.Disconnected += Disconnected;
            bms.NewInputFrame += NewInputFrame;
            bms.Waitbytes += WaitBytes;
            bms.WaitBytesMS = 5;
            bms.OpenGate(false);//Client         
                                //BabylonMS.Util.setPriorityUp();            

            if (TEST_INPUTCONTROLLER)
            {
                hook = new MouseHooker();
            }
            
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
                User32.SetCursorPos(origo.X, origo.Y);   //User32.SetCursorPos(0, 0);
                User32.GetCursorPos(out p);
                virtualOrigoX = p.X;
                virtualOrigoY = p.Y;
                User32.HideMouse();
            }
            public void restoreVirtualMousePosition()
            {
                User32.ShowMouse();
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
                    User32.SetCursorPos(origo.X,origo.Y);
                    MouseEventArgs e2;
                    VirtualMouseX += e.X-origo.X - virtualOrigoX;
                    VirtualMouseY += e.Y-origo.Y - virtualOrigoY;
                    
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
            public Int32 X;
            public Int32 Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }
        public enum SPIF

        {

            None = 0x00,
            /// <summary>Writes the new system-wide parameter setting to the user profile.</summary>
            SPIF_UPDATEINIFILE = 0x01,
            /// <summary>Broadcasts the WM_SETTINGCHANGE message after updating the user profile.</summary>
            SPIF_SENDCHANGE = 0x02,
            /// <summary>Same as SPIF_SENDCHANGE.</summary>
            SPIF_SENDWININICHANGE = 0x02
        }
        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;        // Specifies the size, in bytes, of the structure. 
                                        // The caller must set this to Marshal.SizeOf(typeof(CURSORINFO)).
            public Int32 flags;         // Specifies the cursor state. This parameter can be one of the following values:
                                        //    0             The cursor is hidden.
                                        //    CURSOR_SHOWING    The cursor is showing.
            public IntPtr hCursor;          // Handle to the cursor. 
            public POINT ptScreenPos;       // A POINT structure that receives the screen coordinates of the cursor. 
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool bShow);

        public static uint SPI_SETCURSORS = 0x0057;
        private const uint OCR_NORMAL = 32512;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, SPIF fWinIni); //SPI_SETCURSORS

        [DllImport("user32.dll")]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);


        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursorFromFile(string lpFileName);
        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);
        [DllImport("user32.dll")]
        static extern IntPtr CopyIcon(IntPtr hIcon);

        private static IntPtr cursorHandle;
        private static POINT cursorPosition;
        public static bool mouseVisible= false;
        public static void HideMouse()
        {
            CURSORINFO pci;
            pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            GetCursorInfo(out pci);
            cursorPosition = pci.ptScreenPos;
            cursorHandle = CopyIcon(pci.hCursor);

            IntPtr cursor = LoadCursorFromFile(@"nocursor.cur");
            SetSystemCursor(cursor, OCR_NORMAL);
            mouseVisible = false;
        }
        public static void ShowMouse()
        {
            bool retval = SetSystemCursor(cursorHandle, OCR_NORMAL);
            mouseVisible = true;
        }
    }
}
