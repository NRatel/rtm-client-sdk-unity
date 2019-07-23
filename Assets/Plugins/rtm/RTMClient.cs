using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using GameDevWare.Serialization;
using com.fpnn;

using UnityEngine;

namespace com.rtm {

    public class RTMClient {

        private static class MidGenerator {

            static private long Count = 0;
            static private StringBuilder sb = new StringBuilder(20);
            static private System.Object Lock = new System.Object();

            static public long Gen() {

                lock(Lock) {

                    long c = 0;

                    if (++Count >= 999) {

                        Count = 0;
                    }

                    c = Count;

                    sb.Clear();
                    sb.Append(ThreadPool.Instance.GetMilliTimestamp());

                    if (c < 100) {

                        sb.Append("0");
                    }

                    if (c < 10) {

                        sb.Append("0");
                    }

                    sb.Append(c);
                    return Convert.ToInt64(sb.ToString());
                }
            }
        }

        private FPEvent _event = new FPEvent();

        public FPEvent GetEvent() {

            return this._event;
        }

        private string _dispatch;
        private int _pid;
        private long _uid;
        private string _token;
        private string _version;
        private IDictionary<string, string> _attrs;
        private bool _reconnect;
        private int _timeout;

        private bool _startTimerThread;

        private bool _isClose;
        private string _endpoint;
        private string _switchGate;

        private RTMProcessor _processor;
        private EventDelegate _eventDelegate;

        private BaseClient _baseClient;
        private DispatchClient _dispatchClient;

        /**
         * @param {string}                      dispatch
         * @param {int}                         pid
         * @param {long}                        uid
         * @param {string}                      token
         * @param {string}                      version
         * @param {IDictionary(string,string)}  attrs
         * @param {bool}                        reconnect
         * @param {int}                         timeout
         * @param {bool}                        startTimerThread
         */
        public RTMClient(string dispatch, int pid, long uid, string token, string version, IDictionary<string, string> attrs, bool reconnect, int timeout, bool startTimerThread) {

            Debug.Log("Hello RTM! rtm@" + RTMConfig.VERSION + ", fpnn@" + FPConfig.VERSION);

            this._dispatch = dispatch;
            this._pid = pid;
            this._uid = uid;
            this._token = token;
            this._version = version;
            this._attrs = attrs;
            this._reconnect = reconnect;
            this._timeout = timeout;
            this._startTimerThread = startTimerThread;

            this.InitProcessor();
        }

        private void InitProcessor() {

            RTMClient self = this;

            this._processor = new RTMProcessor(this._event);

            this._processor.AddPushService(RTMConfig.KICKOUT, (data) => {

                self._isClose = true;
                self._baseClient.Close();
            });

            this._eventDelegate = (evd) => {

                // int AvailableWorkerThreads, aiot;
                // System.Threading.ThreadPool.GetAvailableThreads(out AvailableWorkerThreads, out aiot);

                // if (AvailableWorkerThreads <= 1) {

                //     Debug.Log("[ThreadPool] available worker threads: " + AvailableWorkerThreads);
                // }

                long lastPingTimestamp = 0;
                long timestamp = evd.GetTimestamp();

                if (self._processor != null) {

                    lastPingTimestamp = self._processor.GetPingTimestamp();
                }

                if (lastPingTimestamp > 0 && self._baseClient != null && self._baseClient.IsOpen()) {

                    if (timestamp - lastPingTimestamp > RTMConfig.RECV_PING_TIMEOUT) {

                        self._baseClient.Close(new Exception("ping timeout"));
                    }
                }

                self.DelayConnect(timestamp);
            };

            ThreadPool.Instance.SetPool(new RTMThreadPool());
            ThreadPool.Instance.Event.AddListener("second", this._eventDelegate);
        }

        public RTMProcessor GetProcessor() {

            return this._processor;
        }

        public FPPackage GetPackage() {

            if (this._baseClient != null) {

                return this._baseClient.GetPackage();
            }

            return null;
        }

        public void SendQuest(FPData data, CallbackDelegate callback, int timeout) {

            if (this._baseClient != null) {

                this._baseClient.SendQuest(data, this._baseClient.QuestCallback(callback), timeout);
            }
        }

        public CallbackData SendQuest(FPData data, int timeout) {

            if (this._baseClient != null) {

                return this._baseClient.SendQuest(data, timeout);
            }

            return null;
        }

        public void Destroy() {

            this.Close();

            this._reconnCount = 0;
            this._lastConnectTime = 0;

            if (this._baseClient != null) {

                this._baseClient.Destroy();
                this._baseClient = null;
            }

            if (this._dispatchClient != null) {

                this._dispatchClient.Destroy();
                this._dispatchClient = null;
            }

            if (this._processor != null) {

                this._processor.Destroy();
                this._processor = null;
            }

            this._event.RemoveListener();

            if (this._eventDelegate != null) {

                ThreadPool.Instance.Event.RemoveListener("second", this._eventDelegate);
                this._eventDelegate = null;
            }
        }

        /**
         * @param {string}  endpoint
         */
        public void Login(string endpoint) {

            this._endpoint = endpoint;
            this._isClose = false;

            if (!string.IsNullOrEmpty(this._endpoint)) {

                this.ConnectRTMGate(this._timeout);
                return;
            }

            RTMClient self = this;

            if (this._dispatchClient == null) {

                this._dispatchClient = new DispatchClient(this._dispatch, this._timeout, this._startTimerThread);

                this._dispatchClient.GetEvent().AddListener("close", (evd) => {

                    Debug.Log("[DispatchClient] closed!");

                    if (self._dispatchClient != null) {

                        self._dispatchClient.Destroy();
                        self._dispatchClient = null;
                    }

                    if (string.IsNullOrEmpty(self._endpoint)) {

                        self.GetEvent().FireEvent(new EventData("error", new Exception("dispatch client close with err!")));
                        self.Reconnect();
                    }
                });

                this._dispatchClient.GetEvent().AddListener("connect", (evd) => {

                    Debug.Log("[DispatchClient] connected!");

                    IDictionary<string, object> payload = new Dictionary<string, object>();

                    payload.Add("pid", self._pid);
                    payload.Add("uid", self._uid);
                    payload.Add("what", "rtmGated");
                    payload.Add("addrType", self._dispatchClient.IsIPv6() ? "ipv6" : "ipv4");
                    payload.Add("version", self._version);

                    self._dispatchClient.Which(payload, self._timeout, (cbd) => {

                        IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                        if (dict != null) {

                            self.Login(Convert.ToString(dict["endpoint"]));
                        }

                        if (self._dispatchClient != null) {

                            self._dispatchClient.Close(cbd.GetException());
                        }
                    });
                });

                this._dispatchClient.Connect();
            }
        }

        /**
         *
         * rtmGate (2)
         *
         * @param {long}                    to
         * @param {byte}                    mtype
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload 
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendMessage(long to, byte mtype, String msg, String attrs, long mid, int timeout, CallbackDelegate callback) {

            if (mid == 0) {

                mid = MidGenerator.Gen();
            }

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("to", to);
            payload.Add("mid", mid);
            payload.Add("mtype", mtype);
            payload.Add("msg", msg);
            payload.Add("attrs", attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("sendmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                cbd.SetMid(mid);

                if (callback != null) {

                    callback(cbd);
                }
            }, timeout);
        }

        /**
         *
         * rtmGate (3)
         *
         * @param {long}                    gid
         * @param {byte}                    mtype
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload 
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendGroupMessage(long gid, byte mtype, string msg, string attrs, long mid, int timeout, CallbackDelegate callback) {

            if (mid == 0) {

                mid = MidGenerator.Gen();
            }

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("gid", gid);
            payload.Add("mid", mid);
            payload.Add("mtype", mtype);
            payload.Add("msg", msg);
            payload.Add("attrs", attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("sendgroupmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                cbd.SetMid(mid);

                if (callback != null) {

                    callback(cbd);
                }
            }, timeout);
        }

        /**
         *
         * rtmGate (4)
         *
         * @param {long}                    rid
         * @param {byte}                    mtype
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload 
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendRoomMessage(long rid, byte mtype, string msg, string attrs, long mid, int timeout, CallbackDelegate callback) {

            if (mid == 0) {

                mid = MidGenerator.Gen();
            }

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("rid", rid);
            payload.Add("mid", mid);
            payload.Add("mtype", mtype);
            payload.Add("msg", msg);
            payload.Add("attrs", attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("sendroommsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                cbd.SetMid(mid);

                if (callback != null) {

                    callback(cbd);
                }
            }, timeout);
        }

        /**
         *
         * rtmGate (5)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {long}                    mid
         * @param {Exception}               exception
         * @param {IDictionary(p2p:IDictionary(String,int),group:IDictionary(String,int))} payload
         * </CallbackData>
         */
        public void GetUnreadMessage(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getunreadmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (6)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void CleanUnreadMessage(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("cleanunreadmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

         /**
         *
         * rtmGate (7)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {long}                    mid
         * @param {Exception}               exception
         * @param {IDictionary(p2p:Map(String,long),IDictionary:Map(String,long))}    payload
         * </CallbackData>
         */
        public void GetSession(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getsession");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (8)
         *
         * @param {long}                    gid
         * @param {bool}                    desc
         * @param {int}                     num
         * @param {long}                    begin
         * @param {long}                    end
         * @param {long}                    lastid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(num:int,lastid:long,begin:long,end:long,msgs:List(GroupMsg))} payload
         * </CallbackData>
         *
         * <GroupMsg>
         * @param {long}                    id
         * @param {long}                    from
         * @param {byte}                    mtype
         * @param {long}                    mid
         * @param {bool}                    deleted
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mtime
         * </GroupMsg>
         */
        public void GetGroupMessage(long gid, bool desc, int num, long begin, long end, long lastid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("gid", gid);
            payload.Add("desc", desc);
            payload.Add("num", num);

            if (begin > 0) {

                payload.Add("begin", begin);
            }

            if (end > 0) {

                payload.Add("end", end);
            }

            if (lastid > 0) {

                payload.Add("lastid", lastid);
            }

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getgroupmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ol = (List<object>)dict["msgs"];
                    List<IDictionary<string, object>> nl = new List<IDictionary<string, object>>();

                    foreach (List<object> items in ol) {

                        IDictionary<string, object> map = new Dictionary<string, object>();

                        map.Add("id", items[0]);
                        map.Add("from", items[1]);
                        map.Add("mtype", items[2]);
                        map.Add("mid", items[3]);
                        map.Add("deleted", items[4]);
                        map.Add("msg", items[5]);
                        map.Add("attrs", items[6]);
                        map.Add("mtime", items[7]);

                        nl.Add(map);
                    }

                    dict["msgs"] = nl;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (9)
         *
         * @param {long}                    rid
         * @param {bool}                    desc
         * @param {int}                     num
         * @param {long}                    begin
         * @param {long}                    end
         * @param {long}                    lastid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(num:int,lastid:long,begin:long,end:long,msgs:List(RoomMsg))} payload
         * </CallbackData>
         *
         * <RoomMsg>
         * @param {long}                    id
         * @param {long}                    from
         * @param {byte}                    mtype
         * @param {long}                    mid
         * @param {bool}                    deleted
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mtime
         * </RoomMsg>
         */
        public void GetRoomMessage(long rid, bool desc, int num, long begin, long end, long lastid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("rid", rid);
            payload.Add("desc", desc);
            payload.Add("num", num);

            if (begin > 0) {

                payload.Add("begin", begin);
            }

            if (end > 0) {

                payload.Add("end", end);
            }

            if (lastid > 0) {

                payload.Add("lastid", lastid);
            }

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getroommsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ol = (List<object>)dict["msgs"];
                    List<IDictionary<string, object>> nl = new List<IDictionary<string, object>>();

                    foreach (List<object> items in ol) {

                        IDictionary<string, object> map = new Dictionary<string, object>();

                        map.Add("id", items[0]);
                        map.Add("from", items[1]);
                        map.Add("mtype", items[2]);
                        map.Add("mid", items[3]);
                        map.Add("deleted", items[4]);
                        map.Add("msg", items[5]);
                        map.Add("attrs", items[6]);
                        map.Add("mtime", items[7]);

                        nl.Add(map);
                    }

                    dict["msgs"] = nl;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (10)
         *
         * @param {bool}                    desc
         * @param {int}                     num
         * @param {long}                    begin
         * @param {long}                    end
         * @param {long}                    lastid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(num:int,lastid:long,begin:long,end:long,msgs:List(BroadcastMsg))} payload
         * </CallbackData>
         *
         * <BroadcastMsg>
         * @param {long}                    id
         * @param {long}                    from
         * @param {byte}                    mtype
         * @param {long}                    mid
         * @param {bool}                    deleted
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mtime
         * </BroadcastMsg>
         */
        public void GetBroadcastMessage(bool desc, int num, long begin, long end, long lastid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("desc", desc);
            payload.Add("num", num);

            if (begin > 0) {

                payload.Add("begin", begin);
            }

            if (end > 0) {

                payload.Add("end", end);
            }

            if (lastid > 0) {

                payload.Add("lastid", lastid);
            }

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getbroadcastmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ol = (List<object>)dict["msgs"];
                    List<IDictionary<string, object>> nl = new List<IDictionary<string, object>>();

                    foreach (List<object> items in ol) {

                        IDictionary<string, object> map = new Dictionary<string, object>();

                        map.Add("id", items[0]);
                        map.Add("from", items[1]);
                        map.Add("mtype", items[2]);
                        map.Add("mid", items[3]);
                        map.Add("deleted", items[4]);
                        map.Add("msg", items[5]);
                        map.Add("attrs", items[6]);
                        map.Add("mtime", items[7]);

                        nl.Add(map);
                    }

                    dict["msgs"] = nl;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (11)
         *
         * @param {long}                    ouid
         * @param {bool}                    desc
         * @param {int}                     num
         * @param {long}                    begin
         * @param {long}                    end
         * @param {long}                    lastid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(num:int,lastid:long,begin:long,end:long,msgs:List(P2PMsg))} payload
         * </CallbackData>
         *
         * <P2PMsg>
         * @param {long}                    id
         * @param {byte}                    direction
         * @param {byte}                    mtype
         * @param {long}                    mid
         * @param {bool}                    deleted
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {long}                    mtime
         * </P2PMsg>
         */
        public void GetP2PMessage(long ouid, bool desc, int num, long begin, long end, long lastid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("ouid", ouid);
            payload.Add("desc", desc);
            payload.Add("num", num);

            if (begin > 0) {

                payload.Add("begin", begin);
            }

            if (end > 0) {

                payload.Add("end", end);
            }

            if (lastid > 0) {

                payload.Add("lastid", lastid);
            }

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getp2pmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ol = (List<object>)dict["msgs"];
                    List<IDictionary<string, object>> nl = new List<IDictionary<string, object>>();

                    foreach (List<object> items in ol) {

                        IDictionary<string, object> map = new Dictionary<string, object>();

                        map.Add("id", items[0]);
                        map.Add("direction", items[1]);
                        map.Add("mtype", items[2]);
                        map.Add("mid", items[3]);
                        map.Add("deleted", items[4]);
                        map.Add("msg", items[5]);
                        map.Add("attrs", items[6]);
                        map.Add("mtime", items[7]);

                        nl.Add(map);
                    }

                    dict["msgs"] = nl;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (12)
         *
         * @param {string}                  cmd
         * @param {List(long)}              tos
         * @param {long}                    to
         * @param {long}                    rid
         * @param {long}                    gid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(token:string,endpoint:string)}   payload
         * </CallbackData>
         */
        public void FileToken(string cmd, List<long> tos, long to, long rid, long gid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("cmd", cmd);

            if (tos != null && tos.Count > 0) {

                payload.Add("tos", tos);
            }

            if (to > 0) {

                payload.Add("to", to);
            }

            if (rid > 0) {

                payload.Add("rid", rid);
            }

            if (gid > 0) {

                payload.Add("gid", gid);
            }

            this.Filetoken(payload, callback, timeout);
        }

        /**
         * rtmGate (13)
         */
        public void Close() {

            this._isClose = true;

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("bye");
            data.SetPayload(bytes);

            RTMClient self = this;

            this.SendQuest(data, (cbd) => {

                self._baseClient.Close();
            }, 0);
        }

        /**
         *
         * rtmGate (14)
         *
         * @param {IDictionary(string,string)}      attrs
         * @param {int}                             timeout
         * @param {CallbackDelegate}                callback
         *
         * @callback
         * @param {CallbackData}                    cbd
         *
         * <CallbackData>
         * @param {Exception}                       exception
         * @param {IDictionary}                     payload
         * </CallbackData>
         */
        public void AddAttrs(IDictionary<string, string> attrs, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("attrs", attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("addattrs");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (15)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(attrs:List(IDictionary<string, string>))}    payload
         * </CallbackData>
         *
         * <IDictionary<string, string>>
         * @param {string}                  ce
         * @param {string}                  login
         * @param {string}                  my
         * </IDictionary<string, string>>
         */
        public void GetAttrs(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getattrs");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (16)
         *
         * @param {string}                  msg
         * @param {string}                  attrs
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void AddDebugLog(string msg, string attrs, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("msg", msg);
            payload.Add("attrs", attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("adddebuglog");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (17)
         *
         * @param {string}                  apptype
         * @param {string}                  devicetoken
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void AddDevice(string apptype, string devicetoken, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("apptype", apptype);
            payload.Add("devicetoken", devicetoken);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("adddevice");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (18)
         *
         * @param {string}                  devicetoken
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void RemoveDevice(string devicetoken, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("devicetoken", devicetoken);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("removedevice");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (19)
         *
         * @param {string}                  targetLanguage
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void SetTranslationLanguage(string targetLanguage, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("lang", targetLanguage);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("setlang");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (20)
         *
         * @param {string}                  originalMessage
         * @param {string}                  originalLanguage
         * @param {string}                  targetLanguage
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(stext:string,src:string,dtext:string,dst:string)}    payload
         * </CallbackData>
         */
        public void Translate(string originalMessage, string originalLanguage, string targetLanguage, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("text", originalMessage);
            payload.Add("dst", targetLanguage);

            if (!string.IsNullOrEmpty(originalLanguage)) {

                payload.Add("src", originalLanguage);
            }

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("translate");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (21)
         *
         * @param {List(long)}              friends
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void AddFriends(List<long> friends, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("friends", friends);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("addfriends");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (22)
         *
         * @param {List(long)}              friends
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void DeleteFriends(List<long> friends, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("friends", friends);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("delfriends");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (23)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {List(long)}              payload
         * </CallbackData>
         */
        public void GetFriends(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getfriends");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ids = (List<object>)dict["uids"];
                    callback(new CallbackData(ids));
                    return;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (24)
         *
         * @param {long}                    gid
         * @param {List(long)}              uids
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void AddGroupMembers(long gid, List<long> uids, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("gid", gid);
            payload.Add("uids", uids);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("addgroupmembers");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (25)
         *
         * @param {long}                    gid
         * @param {List(long)}              uids
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void DeleteGroupMembers(long gid, List<long> uids, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("gid", gid);
            payload.Add("uids", uids);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("delgroupmembers");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (26)
         *
         * @param {long}                    gid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {List(long)}              payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void GetGroupMembers(long gid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("gid", gid);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getgroupmembers");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ids = (List<object>)dict["uids"];
                    callback(new CallbackData(ids));
                    return;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (27)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {List(long)}              payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void GetUserGroups(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getusergroups");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ids = (List<object>)dict["gids"];
                    callback(new CallbackData(ids));
                    return;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (28)
         *
         * @param {long}                    rid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void EnterRoom(long rid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("rid", rid);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("enterroom");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (29)
         *
         * @param {long}                    rid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void LeaveRoom(long rid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("rid", rid);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("leaveroom");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (30)
         *
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {List(long)}              payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void GetUserRooms(int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getuserrooms");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ids = (List<object>)dict["rooms"];
                    callback(new CallbackData(ids));
                    return;
                }

                callback(cbd);
            }, timeout);
        }

        /**
         *
         * rtmGate (31)
         *
         * @param {List(long)}              uids
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {List(long)}              payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void GetOnlineUsers(List<long> uids, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("uids", uids);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("getonlineusers");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                if (callback == null) {

                    return;
                }

                IDictionary<string, object> dict = (IDictionary<string, object>)cbd.GetPayload();

                if (dict != null) {

                    List<object> ids = (List<object>)dict["uids"];
                    callback(new CallbackData(ids));
                    return;
                }

                callback(cbd);
            }, timeout);            
        }

        /**
         *
         * rtmGate (32)
         *
         * @param {long}                    mid
         * @param {long}                    xid
         * @param {byte}                    type
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void DeleteMessage(long mid, long xid, byte type, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("mid", mid);
            payload.Add("xid", xid);
            payload.Add("type", type);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("delmsg");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (33)
         *
         * @param {string}                  ce
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void Kickout(string ce, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("ce", ce);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("kickout");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (34)
         *
         * @param {string}                  key
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {Exception}               exception
         * @param {IDictionary(val:String)} payload
         * </CallbackData>
         */
        public void DBGet(string key, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("key", key);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("dbget");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * rtmGate (35)
         *
         * @param {string}                  key
         * @param {string}                  value
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary}             payload
         * @param {Exception}               exception
         * </CallbackData>
         */
        public void DBSet(string key, string value, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("key", key);
            payload.Add("val", value);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("dbset");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        /**
         *
         * fileGate (1)
         *
         * @param {byte}                    mtype
         * @param {long}                    to
         * @param {byte[]}                  fileBytes
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendFile(byte mtype, long to, byte[] fileBytes, long mid, int timeout, CallbackDelegate callback) {

            if (fileBytes == null || fileBytes.Length <= 0) {

                this.GetEvent().FireEvent(new EventData("error", new Exception("empty file bytes!")));
                return;
            }

            Hashtable ops = new Hashtable();

            ops.Add("cmd", "sendfile");
            ops.Add("to", to);
            ops.Add("mtype", mtype);
            ops.Add("file", fileBytes);

            this.FileSendProcess(ops, mid, timeout, callback);
        }

        /**
         *
         * fileGate (3)
         *
         * @param {byte}                    mtype
         * @param {long}                    gid
         * @param {byte[]}                  fileBytes
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendGroupFile(byte mtype, long gid, byte[] fileBytes, long mid, int timeout, CallbackDelegate callback) {

            if (fileBytes == null || fileBytes.Length <= 0) {

                this.GetEvent().FireEvent(new EventData("error", new Exception("empty file bytes!")));
                return;
            }

            Hashtable ops = new Hashtable();

            ops.Add("cmd", "sendgroupfile");
            ops.Add("gid", gid);
            ops.Add("mtype", mtype);
            ops.Add("file", fileBytes);

            this.FileSendProcess(ops, mid, timeout, callback);
        }

        /**
         *
         * fileGate (4)
         *
         * @param {byte}                    mtype
         * @param {long}                    rid
         * @param {byte[]}                  fileBytes
         * @param {long}                    mid
         * @param {int}                     timeout
         * @param {CallbackDelegate}        callback
         *
         * @callback
         * @param {CallbackData}            cbd
         *
         * <CallbackData>
         * @param {IDictionary(mtime:long)} payload
         * @param {Exception}               exception
         * @param {long}                    mid
         * </CallbackData>
         */
        public void SendRoomFile(byte mtype, long rid, byte[] fileBytes, long mid, int timeout, CallbackDelegate callback) {

            if (fileBytes == null || fileBytes.Length <= 0) {

                this.GetEvent().FireEvent(new EventData("error", new Exception("empty file bytes!")));
                return;
            }

            Hashtable ops = new Hashtable();

            ops.Add("cmd", "sendroomfile");
            ops.Add("rid", rid);
            ops.Add("mtype", mtype);
            ops.Add("file", fileBytes);

            this.FileSendProcess(ops, mid, timeout, callback);
        }

        /**
         *
         * rtmGate (1)
         *
         */
        private void Auth(int timeout) {

            RTMClient self = this;
            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("pid", this._pid);
            payload.Add("uid", this._uid);
            payload.Add("token", this._token);
            payload.Add("version", this._version);
            payload.Add("attrs", this._attrs);

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("auth");
            data.SetPayload(bytes);

            this.SendQuest(data, (cbd) => {

                Exception exception = cbd.GetException();

                if (exception != null) {

                    if (self._baseClient != null) {

                        self._baseClient.Close(exception);
                    }

                    return;
                }

                object obj = cbd.GetPayload();

                if (obj != null) {

                    IDictionary<string, object> dict = (IDictionary<string, object>)obj;

                    bool ok = Convert.ToBoolean(dict["ok"]);

                    if (ok) {

                        if (self._processor != null) {

                            self._processor.InitPingTimestamp();
                        }

                        self._reconnCount = 0;
                        self.GetEvent().FireEvent(new EventData("login", self._endpoint));
                        return;
                    }

                    if (dict.ContainsKey("gate")) {

                        string gate = Convert.ToString(dict["gate"]);

                        if (!string.IsNullOrEmpty(gate)) {

                            self._switchGate = gate;

                            if (self._baseClient != null) {

                                self._baseClient.Close();
                            }

                            return;
                        }
                    }

                    if (!ok) {

                        self.GetEvent().FireEvent(new EventData("login", new Exception("token error!")));
                        return;
                    }
                }

                if (self._baseClient != null) {

                    self._baseClient.Close(new Exception("auth error!"));
                }
            }, timeout);
        }

        private void ConnectRTMGate(int timeout) {

            RTMClient self = this;

            if (this._baseClient == null) {

                this._baseClient = new BaseClient(this._endpoint, false, timeout, this._startTimerThread);

                this._baseClient.GetEvent().AddListener("connect", (evd) => {

                    self.Auth(timeout);
                });

                this._baseClient.GetEvent().AddListener("close", (evd) => {

                    if (self._baseClient != null) {

                        self._baseClient.Destroy();
                        self._baseClient = null;
                    }

                    self._endpoint = self._switchGate;
                    self._switchGate = null;

                    self.GetEvent().FireEvent(new EventData("close", !self._isClose && self._reconnect));
                    self.Reconnect();
                });

                this._baseClient.GetEvent().AddListener("error", (evd) => {

                    self.GetEvent().FireEvent(new EventData("error", evd.GetException()));
                });

                this._baseClient.GetProcessor().SetProcessor(this._processor);
                this._baseClient.Connect();
            }
        }

        private void FileSendProcess(Hashtable ops, long mid, int timeout, CallbackDelegate callback) {

            IDictionary<string, object> payload = new Dictionary<string, object>();

            payload.Add("cmd", ops["cmd"]);

            if (ops.Contains("tos")) {

                payload.Add("tos", ops["tos"]);
            }

            if (ops.Contains("to")) {

                payload.Add("to", ops["to"]);
            }

            if (ops.Contains("rid")) {

                payload.Add("rid", ops["rid"]);
            }

            if (ops.Contains("gid")) {

                payload.Add("gid", ops["gid"]);
            }

            RTMClient self = this;

            this.Filetoken(payload, (cbd) => {

                Exception exception = cbd.GetException();

                if (exception != null) {

                    self.GetEvent().FireEvent(new EventData("error", exception));
                    return;
                }

                object obj = cbd.GetPayload();

                if (obj != null) {

                    IDictionary<string, object> dict = (IDictionary<string, object>)obj;

                    string token = Convert.ToString(dict["token"]);
                    string endpoint = Convert.ToString(dict["endpoint"]);

                    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(endpoint)) {

                        self.GetEvent().FireEvent(new EventData("error", new Exception("file token error!")));
                        return;
                    }

                    FileClient fileClient = new FileClient(endpoint, timeout, false);

                    dict = new Dictionary<string, object>();

                    dict.Add("pid", self._pid);
                    dict.Add("mtype", ops["mtype"]);
                    dict.Add("mid", mid != 0 ? mid : MidGenerator.Gen());
                    dict.Add("from", self._uid);

                    if (ops.Contains("tos")) {

                        dict.Add("tos", ops["tos"]);
                    }

                    if (ops.Contains("to")) {

                        dict.Add("to", ops["to"]);
                    }

                    if (ops.Contains("rid")) {

                        dict.Add("rid", ops["rid"]);
                    }

                    if (ops.Contains("gid")) {

                        dict.Add("gid", ops["gid"]);
                    }

                    fileClient.Send(Convert.ToString(ops["cmd"]), (byte[])ops["file"], token, dict, timeout, callback);
                }
            }, timeout);
        }

        private void Filetoken(IDictionary<string, object> payload, CallbackDelegate callback, int timeout) {

            byte[] bytes;

            using (MemoryStream outputStream = new MemoryStream()) {

                MsgPack.Serialize(payload, outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                bytes = outputStream.ToArray();
            }

            FPData data = new FPData();
            data.SetFlag(0x1);
            data.SetMtype(0x1);
            data.SetMethod("filetoken");
            data.SetPayload(bytes);

            this.SendQuest(data, callback, timeout);
        }

        private int _reconnCount = 0;

        private void Reconnect() {

            if (!this._reconnect) {

                return;
            }

            if (this._isClose) {

                return;
            }

            if (this._processor != null) {

                this._processor.ClearPingTimestamp();
            }

            if (++this._reconnCount < RTMConfig.RECONN_COUNT_ONCE) {

                this.Login(this._endpoint);
                return;
            }

            this._lastConnectTime = ThreadPool.Instance.GetMilliTimestamp();
        }

        private long _lastConnectTime = 0;

        private void DelayConnect(long timestamp) {

            if (this._lastConnectTime == 0) {

                return;
            }

            if (timestamp - this._lastConnectTime < RTMConfig.CONNCT_INTERVAL) {

                return;
            }

            this._reconnCount = 0;
            this._lastConnectTime = 0;
            this.Login(this._endpoint);
        }

        private class RTMThreadPool:ThreadPool.IThreadPool {

            public RTMThreadPool() {

                System.Threading.ThreadPool.SetMinThreads(2, 1);
                System.Threading.ThreadPool.SetMaxThreads(SystemInfo.processorCount * 2, SystemInfo.processorCount);
            }

            public void Execute(Action<object> action) {

                System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(action));
            }
        }

        private class DispatchClient:BaseClient {

            public DispatchClient(string endpoint, int timeout, bool startTimerThread):base(endpoint, false, timeout, startTimerThread) {}
            public DispatchClient(string host, int port, int timeout, bool startTimerThread):base(host, port, false, timeout, startTimerThread) {}

            public override void AddListener() {

                base.GetEvent().AddListener("error", (evd) => {

                    Debug.Log(evd.GetException().Message);
                });
            }

            public void Which(IDictionary<string, object> payload, int timeout, CallbackDelegate callback) {

                byte[] bytes;

                using (MemoryStream outputStream = new MemoryStream()) {

                    MsgPack.Serialize(payload, outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    bytes = outputStream.ToArray();
                } 

                FPData data = new FPData();
                data.SetFlag(0x1);
                data.SetMtype(0x1);
                data.SetMethod("which");
                data.SetPayload(bytes);

                base.SendQuest(data, base.QuestCallback(callback), timeout);
            }
        }

        private class FileClient:BaseClient {

            public FileClient(string endpoint, int timeout, bool startTimerThread):base(endpoint, false, timeout, startTimerThread) {}
            public FileClient(string host, int port, int timeout, bool startTimerThread):base(host, port, false, timeout, startTimerThread) {}

            public override void AddListener() {

                base.GetEvent().AddListener("connect", (evd) => {

                    Debug.Log("[FileClient] connected!");
                });

                base.GetEvent().AddListener("close", (evd) => {

                    Debug.Log("[FileClient] closed!");
                });

                base.GetEvent().AddListener("error", (evd) => {

                    Debug.Log(evd.GetException().Message);
                });
            }

            public void Send(string method, byte[] fileBytes, string token, IDictionary<string, object> payload, int timeout, CallbackDelegate callback) {

                String fileMd5 = base.CalcMd5(fileBytes, false);
                String sign = base.CalcMd5(fileMd5 + ":" + token, false);

                if (string.IsNullOrEmpty(sign)) {

                    base.GetEvent().FireEvent(new EventData("error", new Exception("wrong sign!")));
                    return;
                }

                if (!base.HasConnect()) {

                    base.Connect();
                }

                IDictionary<string, string> attrs = new Dictionary<string, string>();
                attrs.Add("sign", sign);

                payload.Add("token", token);
                payload.Add("file", fileBytes);
                payload.Add("attrs", Json.SerializeToString(attrs));

                long mid = (long)Convert.ToInt64(payload["mid"]);

                byte[] bytes;

                using (MemoryStream outputStream = new MemoryStream()) {

                    MsgPack.Serialize(payload, outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    bytes = outputStream.ToArray();
                }

                FPData data = new FPData();
                data.SetFlag(0x1);
                data.SetMtype(0x1);
                data.SetMethod(method);
                data.SetPayload(bytes);

                FileClient self = this;

                base.SendQuest(data, base.QuestCallback((cbd) => {

                    cbd.SetMid(mid);
                    self.Destroy();

                    if (callback != null) {

                        callback(cbd);
                    }
                }), timeout);
            }
        }

        private class BaseClient:FPClient {

            public BaseClient(string endpoint, bool reconnect, int timeout, bool startTimerThread):base(endpoint, reconnect, timeout) {

                if (startTimerThread) {

                    ThreadPool.Instance.StartTimerThread();
                }

                this.AddListener();
            }

            public BaseClient(string host, int port, bool reconnect, int timeout, bool startTimerThread):base(host, port, reconnect, timeout) {

                if (startTimerThread) {

                    ThreadPool.Instance.StartTimerThread();
                }

                this.AddListener();
            }

            public virtual void AddListener() {}

            public string CalcMd5(string str, bool upper) {

                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(str);
                return CalcMd5(inputBytes, upper);
            }

            public string CalcMd5(byte[] bytes, bool upper) {

                MD5 md5 = System.Security.Cryptography.MD5.Create();
                byte[] hash = md5.ComputeHash(bytes);
                
                string f = "x2";

                if (upper) {

                    f = "X2";
                }

                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < hash.Length; i++) {

                    sb.Append(hash[i].ToString(f));
                }

                return sb.ToString();
            }

            private void CheckFPCallback(CallbackData cbd) {

                bool isAnswerException = false;
                FPData data = cbd.GetData();
                IDictionary<string, object> payload = null;

                if (data != null) {

                    if (data.GetFlag() == 0) {

                        try {

                            payload = Json.Deserialize<IDictionary<string, object>>(data.JsonPayload());
                        }catch(Exception ex) {

                           base.GetEvent().FireEvent(new EventData("error", ex)); 
                        }
                    }

                    if (data.GetFlag() == 1) {

                        try {

                            using (MemoryStream inputStream = new MemoryStream(data.MsgpackPayload())) {

                                payload = MsgPack.Deserialize<IDictionary<string, object>>(inputStream);
                            }
                        } catch(Exception ex) {

                           base.GetEvent().FireEvent(new EventData("error", ex));
                        }
                    }

                    if (base.GetPackage().IsAnswer(data)) {

                        isAnswerException = data.GetSS() != 0;
                    }
                }

                cbd.CheckException(isAnswerException, payload);
            }

            public CallbackDelegate QuestCallback(CallbackDelegate callback) {

                BaseClient self = this;

                return (cbd) => {

                    if (callback == null) {

                        return;
                    }

                    self.CheckFPCallback(cbd);
                    callback(cbd);
                };
            }
        }
    }
}