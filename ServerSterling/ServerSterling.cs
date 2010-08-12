﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TradeLink.API;
using TradeLink.Common;
using System.Threading;
using SterlingLib;
namespace SterServer
{

    public class ServerSterling 
    {
        public const string PROGRAM ="ServerSterling";
        Thread _bw;
        // basic structures needed for operation
        STIEvents stiEvents;
        STIOrderMaint stiOrder;
        STIPosition stiPos;
        STIQuote stiQuote;
        STIBook stiBook;
        TradeLinkServer tl;

        bool _ignoreoutoforderticks = true;
        public bool IgnoreOutOfOrderTicks { get { return _ignoreoutoforderticks; } set { _ignoreoutoforderticks = value; } }
        int _fixorderdecimalplace = 2;
        public int FixOrderDecimalPlace { get { return _fixorderdecimalplace; } set { _fixorderdecimalplace = value; } }


        PositionTracker pt = new PositionTracker();
        int _SLEEP = 50;
        int _ORDERSLEEP = 1;
        int _CANCELWAIT = 1000;
        public int CancelWait { get { return _CANCELWAIT; } set { _CANCELWAIT = value; } }
        bool _supportcover = true;
        public bool CoverEnabled { get { return _supportcover; } set { _supportcover = value; } }
        
        public ServerSterling(TradeLinkServer tls, int sleepOnNodata, int sleepAfterOrder, DebugDelegate deb)
        {
            SendDebug = deb;
            tl = tls;
            _SLEEP = 50;
            _ORDERSLEEP = sleepAfterOrder;
            Start();
        }
        bool _connected = false;
        public bool isConnected { get { return _connected; } }
        bool _verbosedebug = false;
        public bool VerboseDebugging { get { return _verbosedebug; } set { _verbosedebug = value; } }
        public bool Start()
        {
            try
            {
                if (_connected) return true;
                debug(Util.TLSIdentity());
                debug("Attempting to start: " + PROGRAM);
                // basic structures needed for operation
                stiEvents = new STIEvents();
                stiOrder = new STIOrderMaint();
                stiPos = new STIPosition();
                stiQuote = new STIQuote();
                stiBook = new STIBook();
                _bw = new Thread(new ParameterizedThreadStart(background));
                _runbg = true;
                _bw.Start();

                stiEvents.OnSTIShutdown += new _ISTIEventsEvents_OnSTIShutdownEventHandler(stiEvents_OnSTIShutdown);
                stiEvents.SetOrderEventsAsStructs(true);

                stiEvents.OnSTIOrderUpdate += new _ISTIEventsEvents_OnSTIOrderUpdateEventHandler(stiEvents_OnSTIOrderUpdate);
                stiEvents.OnSTITradeUpdate += new _ISTIEventsEvents_OnSTITradeUpdateEventHandler(stiEvents_OnSTITradeUpdate);
                stiPos.OnSTIPositionUpdate += new _ISTIPositionEvents_OnSTIPositionUpdateEventHandler(stiPos_OnSTIPositionUpdate);
                stiQuote.OnSTIQuoteUpdate += new _ISTIQuoteEvents_OnSTIQuoteUpdateEventHandler(stiQuote_OnSTIQuoteUpdate);
                stiQuote.OnSTIQuoteSnap += new _ISTIQuoteEvents_OnSTIQuoteSnapEventHandler(stiQuote_OnSTIQuoteSnap);
                stiEvents.OnSTIOrderRejectMsg += new _ISTIEventsEvents_OnSTIOrderRejectMsgEventHandler(stiEvents_OnSTIOrderRejectMsg);
                stiEvents.OnSTIOrderReject += new _ISTIEventsEvents_OnSTIOrderRejectEventHandler(stiEvents_OnSTIOrderReject);
                stiPos.GetCurrentPositions();

                tl.newAcctRequest += new StringDelegate(tl_newAcctRequest);
                tl.newProviderName = Providers.Sterling;
                tl.newSendOrderRequest += new OrderDelegateStatus(tl_gotSrvFillRequest);
                tl.newPosList += new PositionArrayDelegate(tl_gotSrvPosList);
                tl.newRegisterStocks += new DebugDelegate(tl_RegisterStocks);
                tl.newOrderCancelRequest += new LongDelegate(tl_OrderCancelRequest);
                tl.newFeatureRequest += new MessageArrayDelegate(tl_newFeatureRequest);
                tl.newUnknownRequest += new UnknownMessageDelegate(tl_newUnknownRequest);
                tl.newImbalanceRequest += new VoidDelegate(tl_newImbalanceRequest);
            }
            catch (Exception ex)
            {
                debug(ex.Message + ex.StackTrace);
                _connected = false;
                return false;
            }
            debug(PROGRAM + " started.");
            _connected = true;
            return _connected;
        }

        void stiEvents_OnSTIShutdown()
        {
            debug("Interface shutdown");
        }

        void stiEvents_OnSTIOrderReject(ref structSTIOrderReject structOrderReject)
        {
            debug("reject: " + structOrderReject.bstrClOrderId + " reason: " + structOrderReject.nRejectReason+ " "+sterrejectpretty(structOrderReject.nRejectReason));
        }

        void stiEvents_OnSTIOrderRejectMsg(STIOrderRejectMsg oSTIOrderRejectMsg)
        {
            debug("reject: " + oSTIOrderRejectMsg.ClOrderID + " reason: " + oSTIOrderRejectMsg.RejectReason.ToString());
        }

        string sterrejectpretty(string rint)
        {
            int ri = -1;
            if (int.TryParse(rint, out ri))
                return sterrejectpretty(ri);
            return "unknown reject error";
        }
        string sterrejectpretty(int r)
        {
            try
            {
                rejectmessages rm = (rejectmessages)r;
                return rm.ToString();
            }
            catch (Exception)
            {
                return "unknown reject error";
            }
        }

        enum rejectmessages
        {
            rrSTIUnknown = 0,
            rrSTIUnknownPid =1,
            rrSTIInvalidPassword,
            rrSTIAccessDenied,
            rrSTINotFound,
            rrSTICannotRoute,
            rrSTIPendingCancel,
            rrSTIPendingReplace,
            rrSTIOrderClosed,
            rrSTICannotCreate,
            rrSTIDupeClOrdId,
            rrSTINoSeqNoAvailable,
            rrSTIInvalidAcct,
            rrSTIInvalidDest_OrNotEnabledForDest,
            rrSTIError,
            rrSTIDupeSeqNo,
            rrSTINoChange,
            rrSTIInvalidSeqNo,
            rrSTIInvalidQty,
            rrSTITLTC_TooLateToCancel,
            rrSTIShareLimit,
            rrSTIDollarLimit,
            rrSTIBuyingPower,
            rrSTITenSecRule,
            rrSTINotSupported,
            rrSTIDupeAcct,
            rrSTIInvalidGroupId,
            rrSTIDupeStation,
            rrSTIPosTradingLmt,
            rrSTITltcMoc_TooLateCancelMOC,
            rrSTIHardToBorrow,
            rrSTIVersion,
            rrSTIDupeLogin,
            rrSTIInvalidSym,
            rrSTINxRules,
            rrSTIBulletNotRequired,
            rrSTIMocMktImb,
            rrSTINx30SecRule,
            rrSTIEasyToBorrowOnly,
            rrSTIStaleOrder,
            rrSTILast,
        }

        bool _lastimbalance = false;
        bool _imbalance = false;
        void tl_newImbalanceRequest()
        {
            _imbalance = true;
        }

        string tl_newAcctRequest()
        {
            return string.Join(",", accts.ToArray());
        }

        long tl_newUnknownRequest(MessageTypes t, string msg)
        {
            if (VerboseDebugging)
                debug("got message: " + t.ToString() + " " + msg);
            // message will be handled on main thread for com security
            _msgq.Write(new GenericMessage(t, msg));
            // we say ok for any supported messages
            switch (t)
            {
                case MessageTypes.SENDORDERPEGMIDPOINT:
                    return (long)MessageTypes.OK;
            }
            return (long)MessageTypes.UNKNOWN_MESSAGE;
        }

        MessageTypes[] tl_newFeatureRequest()
        {
            List<MessageTypes> f = new List<MessageTypes>();
            f.Add(MessageTypes.LIVEDATA);
            f.Add(MessageTypes.LIVETRADING);
            f.Add(MessageTypes.SIMTRADING);
            f.Add(MessageTypes.ORDERCANCELREQUEST);
            f.Add(MessageTypes.ORDERCANCELRESPONSE);
            f.Add(MessageTypes.OK);
            f.Add(MessageTypes.BROKERNAME);
            f.Add(MessageTypes.CLEARCLIENT);
            f.Add(MessageTypes.CLEARSTOCKS);
            f.Add(MessageTypes.FEATUREREQUEST);
            f.Add(MessageTypes.FEATURERESPONSE);
            f.Add(MessageTypes.HEARTBEAT);
            f.Add(MessageTypes.ORDERNOTIFY);
            f.Add(MessageTypes.EXECUTENOTIFY);
            f.Add(MessageTypes.REGISTERCLIENT);
            f.Add(MessageTypes.REGISTERSTOCK);
            f.Add(MessageTypes.TICKNOTIFY);
            f.Add(MessageTypes.VERSION);
            f.Add(MessageTypes.IMBALANCEREQUEST);
            f.Add(MessageTypes.IMBALANCERESPONSE);
            f.Add(MessageTypes.POSITIONREQUEST);
            f.Add(MessageTypes.POSITIONRESPONSE);
            f.Add(MessageTypes.ACCOUNTREQUEST);
            f.Add(MessageTypes.ACCOUNTRESPONSE);
            f.Add(MessageTypes.SENDORDER);
            f.Add(MessageTypes.SENDORDERSTOP);
            f.Add(MessageTypes.SENDORDERMARKET);
            f.Add(MessageTypes.SENDORDERLIMIT);
            f.Add(MessageTypes.SENDORDERTRAIL);
            f.Add(MessageTypes.SENDORDERPEGMIDPOINT);
            return f.ToArray();
        }



        public void Stop()
        {
            try
            {
                _runbg = false;
                stiQuote.DeRegisterAllQuotes();
                stiBook = null;
                stiOrder = null;
                stiPos = null;
                stiEvents = null;
                stiQuote = null;
                if ((_bw.ThreadState != ThreadState.Aborted) || (_bw.ThreadState != ThreadState.Stopped))
                {
                    try
                    {
                        _bw.Abort();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                debug(ex.Message + ex.StackTrace);
            }
            if (tl != null)
                tl.Stop();

        }
        const int MAXRECORD = 5000;
        RingBuffer<Order> _orderq = new RingBuffer<Order>(MAXRECORD);
        RingBuffer<long> _cancelq = new RingBuffer<long>(MAXRECORD);
        RingBuffer<bool> _symsq = new RingBuffer<bool>(5);
        RingBuffer<GenericMessage> _msgq = new RingBuffer<GenericMessage>(100);
        string symquotes = "";
        Dictionary<long, string> idacct = new Dictionary<long, string>();

        string _account = string.Empty;
        /// <summary>
        /// gets or sets default account
        /// </summary>
        public string Account { get { return _account; } set { _account = value; debug("default account: " + _account); } }

        bool _autosetunsetid = true;

        public bool AutosetUnsetId { get { return _autosetunsetid; } set { _autosetunsetid = value; } }


        bool _runbg = false;
        void background(object param)
        {
            while (_runbg)
            {
                try
                {
                    // orders
                    while (!_orderq.isEmpty)
                    {
                        STIOrder order = new STIOrder();
                        Order o = _orderq.Read();
                        if (VerboseDebugging)
                            debug("client order received: " + o.ToString());
                        if ((o.id == 0) && AutosetUnsetId)
                        {
                            o.id = _idt.AssignId;
                        }
                        o.price = Math.Round(o.price, FixOrderDecimalPlace);
                        o.stopp = Math.Round(o.stopp, FixOrderDecimalPlace);
                        order.LmtPrice = (double)o.price;
                        order.StpPrice = (double)o.stopp;
                        if (o.ex == string.Empty)
                            o.ex = o.symbol.Length > 3 ? "NSDQ" : "NYSE";
                        order.Destination = o.Exchange;
                        order.Side = getside(o.symbol,o.side);
                        order.Symbol = o.symbol;
                        order.Quantity = o.UnsignedSize;
                        string acct = _account != string.Empty ? _account : (accts.Count > 0 ? accts[0] : string.Empty);
                        order.Account = o.Account != string.Empty ? o.Account : acct;
                        order.Destination = o.Exchange != "" ? o.ex : "NYSE";
                        bool close = o.TIF == "CLS";
                        order.Tif = tif2tif(o.TIF);
                        if (close)
                        {
                            if (o.isMarket)
                                order.PriceType = STIPriceTypes.ptSTIMktClo;
                            else if (o.isLimit)
                                order.PriceType = STIPriceTypes.ptSTILmtClo;
                            else
                                order.PriceType = STIPriceTypes.ptSTIClo;
                        }
                        else if (o.isMarket)
                            order.PriceType = STIPriceTypes.ptSTIMkt;
                        else if (o.isLimit && o.isStop)
                            order.PriceType = STIPriceTypes.ptSTISvrStpLmt;
                        else if (o.isLimit)
                            order.PriceType = STIPriceTypes.ptSTILmt;
                        else if (o.isStop)
                            order.PriceType = STIPriceTypes.ptSTISvrStp;
                        else if (o.isTrail)
                            order.PriceType = STIPriceTypes.ptSTITrailStp;
                        order.ClOrderID = o.id.ToString();
                        int err = order.SubmitOrder();
                        if (VerboseDebugging)
                            debug("client order sent: " + order.ClOrderID);
                        string tmp = "";
                        if ((err == 0) && (!idacct.TryGetValue(o.id, out tmp)))
                        {
                            // save account/id relationship for canceling
                            idacct.Add(o.id, order.Account);
                            // wait briefly between orders
                            Thread.Sleep(_ORDERSLEEP);
                        }
                        if (err < 0)
                            debug("Error sending order: " + Util.PrettyError(tl.newProviderName, err) + o.ToString());
                        if (err == -1)
                            debug("Make sure you have set the account in sending program.");
                    }

                    // quotes
                    if (!_symsq.isEmpty)
                    {
                        _symsq.Read();
                        foreach (string sym in symquotes.Split(','))
                            stiQuote.RegisterQuote(sym, "*");
                    }

                    // cancels
                    if (!_cancelq.isEmpty)
                    {
                        long number = _cancelq.Read();
                        string acct = "";
                        if (idacct.TryGetValue(number, out acct))
                        {
                            // get unique cancel id
                            long cancelid = _canceltracker.AssignId;
                            // save cancel to order id relationship
                            _cancel2order.Add(cancelid, number);
                            // send cancel
                            stiOrder.CancelOrder(acct, 0, number.ToString(), cancelid.ToString());
                            if (VerboseDebugging)
                                debug("client cancel requested: " + number.ToString() + " " + cancelid.ToString());
                        }
                        else
                            debug("No record of id: " + number.ToString());
                        // see if empty yet
                        if (_cancelq.hasItems)
                            Thread.Sleep(_CANCELWAIT);
                    }

                    // messages
                    if (_msgq.hasItems)
                    {
                        GenericMessage gm = _msgq.Read();
                        switch (gm.Type)
                        {
                            case MessageTypes.SENDORDERPEGMIDPOINT:
                                {
                                    // create order
                                    STIOrder order = new STIOrder();
                                    // pegged 2 midmarket
                                    order.ExecInst = "M";
                                    // get order
                                    Peg2Midpoint o = Peg2Midpoint.Deserialize(gm.Request);
                                    if (!o.isValid) break;
                                    if (VerboseDebugging)
                                        debug("client P2M order: " + o.ToString());
                                    order.Symbol = o.symbol;
                                    order.PegDiff = (double)o.pegdiff;
                                    order.PriceType = STIPriceTypes.ptSTIPegged;
                                    bool side = o.size > 0;
                                    order.Side = getside(o.symbol, side);
                                    order.Quantity = Math.Abs(o.size);
                                    order.Destination = o.ex;
                                    order.ClOrderID = o.id.ToString();
                                    string acct = _account != string.Empty ? _account : (accts.Count > 0 ? accts[0] : string.Empty);
                                    order.Account = o.Account != string.Empty ? o.Account : acct;
                                    int err = order.SubmitOrder();
                                    string tmp = "";
                                    if ((err == 0) && (!idacct.TryGetValue(o.id, out tmp)))
                                        idacct.Add(o.id, order.Account);
                                    if (err < 0)
                                        debug("Error sending order: " + Util.PrettyError(tl.newProviderName, err) + o.ToString());
                                    if (err == -1)
                                        debug("Make sure you have set the account in sending program.");

                                }
                                break;
                        }
                    }

                    if (_lastimbalance != _imbalance)
                    {
                        _lastimbalance = _imbalance;
                        // register for imbalance data
                        stiQuote.RegisterForAllMdx(true);
                    }
                }
                catch (Exception ex)
                {
                    debug(ex.Message + ex.StackTrace);
                }
                if (_symsq.isEmpty && _orderq.isEmpty && _cancelq.isEmpty)
                    Thread.Sleep(_SLEEP);
            }
        }

        string tif2tif(string incoming)
        {
            if ((incoming == "OPG") || (incoming == "OPN"))
            {
                return "O";
            }
            if (incoming == "CLS")
            {
                return string.Empty;
            }
            return incoming;
        }

        Dictionary<long, long> _cancel2order = new Dictionary<long, long>(MAXRECORD);

        IdTracker _canceltracker = new IdTracker(false, 0, DateTime.Now.Ticks);

        string getside(string symbol, bool side)
        {
            // use by and sell as default
            string r = side ? "B" : "S";
            if (CoverEnabled)
            {
                // if we're flat or short and selling, mark as a short
                if ((pt[symbol].isFlat || pt[symbol].isShort) && !side)
                    r = "T";
                // if short and buying, mark as cover
                else if (pt[symbol].isShort && side)
                    r = "C";
            }
            return r;
        }

        void tl_RegisterStocks(string msg)
        {
            if (VerboseDebugging)
                debug("client subscribe request received: " + msg);
            symquotes = msg;
            _symsq.Write(true);
        }


        void tl_OrderCancelRequest(long number)
        {
            _cancelq.Write(number);
        }



        void stiEvents_OnSTITradeUpdate(ref structSTITradeUpdate t)
        {
            Trade f = new TradeImpl();
            f.symbol = t.bstrSymbol;
            f.Account = t.bstrAccount;
            long id = 0;
            if (long.TryParse(t.bstrClOrderId, out id))
                f.id = id;
            f.xprice = (decimal)t.fExecPrice;
            f.xsize = t.nQuantity;
            long now = Convert.ToInt64(t.bstrUpdateTime);
            int xsec = (int)(now % 100);
            long rem = (now - xsec) / 100;
            f.side = t.bstrSide == "B";
            f.xtime = ((int)(rem % 10000)) * 100 + xsec;
            f.xdate = (int)((now - f.xtime) / 1000000);
            f.ex = t.bstrDestination;
            pt.Adjust(f);
            tl.newFill(f);
            if (VerboseDebugging)
                debug("new trade sent: " + f.ToString() + " " + f.id);
        }

        List<long> _onotified = new List<long>(MAXRECORD);

        void stiEvents_OnSTIOrderUpdate(ref structSTIOrderUpdate structOrderUpdate)
        {
            Order o = new OrderImpl();
            o.symbol = structOrderUpdate.bstrSymbol;
            long id = 0;
            if (!long.TryParse(structOrderUpdate.bstrClOrderId, out id))
                id = (long)structOrderUpdate.nOrderRecordId;
            // if this is a cancel notification, pass along
            if (structOrderUpdate.nOrderStatus == (int)STIOrderStatus.osSTICanceled)
            {
                // if it's a cancel, we'll have cancel id rather than order id
                // get new id
                long orderid = 0;
                if (_cancel2order.TryGetValue(id, out orderid))
                {
                    tl.newCancel(orderid);
                    if (VerboseDebugging)
                        debug("cancel received for: " + orderid);
                }
                else
                    debug("no record for cancel id: " + id);
                return;
            }
            // don't notify for same order more than once
            if (_onotified.Contains(id)) return;
            if (structOrderUpdate.bstrLogMessage.Contains("REJ"))
                debug(id+" "+structOrderUpdate.bstrLogMessage);
            o.id = id;
            o.size = structOrderUpdate.nQuantity;
            o.side = structOrderUpdate.bstrSide == "B";
            o.price = (decimal)structOrderUpdate.fLmtPrice;
            o.stopp = (decimal)structOrderUpdate.fStpPrice;
            o.TIF = structOrderUpdate.bstrTif;
            o.Account = structOrderUpdate.bstrAccount;
            o.ex = structOrderUpdate.bstrDestination;
            long now = Convert.ToInt64(structOrderUpdate.bstrUpdateTime);
            int xsec = (int)(now % 100);
            long rem = (now - xsec) / 100;
            o.time = ((int)(rem % 10000)) * 100 + xsec;
            o.date = (int)((rem - o.time) / 10000);
            _onotified.Add(o.id);
            if (VerboseDebugging)
                debug("order acknowledgement: " + o.ToString());
            tl.newOrder(o);
            
        }



        Position[] tl_gotSrvPosList(string account)
        {
            return pt.ToArray();
        }

        int _lasttime = 0;

        void stiQuote_OnSTIQuoteUpdate(ref structSTIQuoteUpdate q)
        {
            Tick k = new TickImpl(q.bstrSymbol);
            k.bid = (decimal)q.fBidPrice;
            k.ask = (decimal)q.fAskPrice;
            k.bs = q.nBidSize / 100;
            k.os = q.nAskSize / 100;
            k.ex = GetExPretty(q.bstrExch);
            k.be = GetExPretty(q.bstrBidExch);
            k.oe = GetExPretty(q.bstrAskExch);
            int now = Convert.ToInt32(q.bstrUpdateTime);
            k.date = Util.ToTLDate(DateTime.Now);
            //int sec = now % 100;
            k.time = now;
            if (IgnoreOutOfOrderTicks && (k.time < _lasttime)) return;
            _lasttime = k.time;
            k.trade = (decimal)q.fLastPrice;
            k.size = q.nLastSize;
            if (!_imbalance || (_imbalance && k.isValid))
                tl.newTick(k);
            // if imbalances are not enabled we're done
            if (!_imbalance) return;
            // if there is no imbalance we're done
            if (q.nMktImbalance==0) return;
            Imbalance imb = new ImbalanceImpl(k.symbol, GetExPretty(k.ex), q.nMktImbalance, k.time, 0, 0, q.nMktImbalance);
            tl.newImbalance(imb);

        }

        void stiQuote_OnSTIQuoteSnap(ref structSTIQuoteSnap q)
        {
            TickImpl k = new TickImpl();
            k.symbol = q.bstrSymbol;
            k.bid = (decimal)q.fBidPrice;
            k.ask = (decimal)q.fAskPrice;
            k.bs = q.nBidSize / 100;
            k.os = q.nAskSize / 100;
            k.ex = GetExPretty(q.bstrExch);
            k.be = GetExPretty(q.bstrBidExch);
            k.oe = GetExPretty(q.bstrAskExch);
            int now = Convert.ToInt32(q.bstrUpdateTime);
            k.date = Util.ToTLDate(DateTime.Now);
            k.time = now;
            k.trade = (decimal)q.fLastPrice;
            k.size = q.nLastSize;
            tl.newTick(k);
        }
        IdTracker _idt = new IdTracker();
        long tl_gotSrvFillRequest(Order o)
        {

            if (o.id == 0) o.id = _idt.AssignId;
            _orderq.Write(o);
            return (long)MessageTypes.OK;
        }

        List<string> accts = new List<string>();
        void stiPos_OnSTIPositionUpdate(ref structSTIPositionUpdate structPositionUpdate)
        {
            // symbol
            string sym = structPositionUpdate.bstrSym;
            // size
            int size = structPositionUpdate.nSharesBot - structPositionUpdate.nSharesSld + structPositionUpdate.nOpeningPosition;
            // price
            decimal price = Math.Abs((decimal)structPositionUpdate.fPositionCost / size);
            // closed pl
            decimal cpl = (decimal)structPositionUpdate.fReal;
            // account
            string ac = structPositionUpdate.bstrAcct;
            // build position
            Position p = new PositionImpl(sym, price, size, cpl, ac);
            // track it
            pt.NewPosition(p);
            // track account
            if (!accts.Contains(ac))
                accts.Add(ac);
            if (VerboseDebugging)
                debug("new position sent: " + p.ToString());
        }

        void stiEvents_OnSTIOrderUpdateMsg(STIOrderUpdateMsg oSTIOrderUpdateMsg)
        {
            throw new NotImplementedException();
        }

        void stiEvents_OnSTIOrderConfirmMsg(STIOrderConfirmMsg oSTIOrderConfirmMsg)
        {
            throw new NotImplementedException();
        }

        public string GetExPretty(string val)
        {
            return GetExType(val).ToString();
        }

        public STEREXCH GetExType(string val)
        {
            try
            {
                char c = val.ToCharArray(0, 1)[0];
                int ascii = (int)c;
                return (STEREXCH)ascii;
            }
            catch { }
            return STEREXCH.NONE;
        }

        void debug(string msg)
        {
            if (SendDebug != null)
                SendDebug(msg);
        }

        public event DebugDelegate SendDebug;
    }

    public enum STEREXCH
    {
        NONE = -1,
        AMEX = 65,
        BSTN = 66,
        CNCI = 67,
        MWST = 77,
        NYSE = 78,
        PACE = 79,
        NSDS = 83,
        NSDT = 84,
        NSDQ = 81,
        CBOE = 87,
        PSE = 88,
        CMPO = 79,
        CMPE = 42,
    }

}
