using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Scada.Data;
using StriderMqtt;


namespace Scada.Comm.Devices
{
	public class KpMQTTLogic : KPLogic
	{
		private IMqttTransport Transport;
		private IMqttPersistence Persistence;

		private int Keepalive;
		private int LastRead;
		private int LastWrite;

		public bool IsSessionPresent { get; private set; }
		public bool IsPublishing { get; private set; }
		public bool InterruptLoop { get; set; }

		private MqttConnectionArgs connArgs;

		public KpMQTTLogic(int number) : base(number)
		{
			ConnRequired = false;
			WorkState = WorkStates.Normal;
		}

		private void ResumeOutgoingFlows(){

			foreach (OutgoingFlow flow in Persistence.GetPendingOutgoingFlows()){
				Resume(flow);
			}
		}

		private void Resume(OutgoingFlow flow){
			if (flow.Qos == MqttQos.AtLeastOnce || (flow.Qos == MqttQos.ExactlyOnce && !flow.Received)){
				PublishPacket publish = new PublishPacket () {
					PacketId = flow.PacketId,
					QosLevel = flow.Qos,
					Topic = flow.Topic,
					Message = flow.Payload,
					DupFlag = true
				};
				Publish (publish);
			}
			else if (flow.Qos == MqttQos.ExactlyOnce && flow.Received)
			{
				Pubrel(flow.PacketId);
			}
			Persistence.LastOutgoingPacketId = flow.PacketId;
		}

		public ushort Publish(PublishPacket packet){

			if (packet.QosLevel != MqttQos.AtMostOnce){
				if (packet.PacketId == 0){
					packet.PacketId = this.GetNextPacketId ();
				}
				Persistence.RegisterOutgoingFlow (new OutgoingFlow () {
					PacketId = packet.PacketId,
					Topic = packet.Topic,
					Qos = packet.QosLevel,
					Payload = packet.Message
				});
			}

			try{
				IsPublishing = true;
				Send(packet);
				return packet.PacketId;
			}
			catch{
				IsPublishing = false;
				throw;
			}
		}

		private void Pubrel(ushort packetId)
		{
			try
			{
				IsPublishing = true;
				Send(new PubrelPacket() { PacketId = packetId });
			}
			catch
			{
				IsPublishing = false;
				throw;
			}
		}

		public void Subscribe(SubscribePacket packet)
		{
			if (packet.PacketId == 0)
			{
				packet.PacketId = this.GetNextPacketId();
			}

			Send(packet);
		}

		public void Unsubscribe(UnsubscribePacket packet)
		{
			if (packet.PacketId == 0)
			{
				packet.PacketId = this.GetNextPacketId();
			}

			Send(packet);
		}


		private ConnectPacket MakeConnectMessage(MqttConnectionArgs args){
			ConnectPacket conn = new ConnectPacket ();
			conn.ProtocolVersion = args.Version;

			conn.ClientId = args.ClientId;
			conn.Username = args.Username;
			conn.Password = args.Password;

			if(args.WillMessage !=null){
				conn.WillFlag = true;
				conn.WillTopic = args.WillMessage.Topic;
				conn.WillMessage = args.WillMessage.Message;
				conn.WillQosLevel = args.WillMessage.Qos;
				conn.WillRetain = args.WillMessage.Retain;
			}

			conn.CleanSession = args.CleanSession;
			conn.KeepAlivePeriod = (ushort)args.Keepalive.TotalSeconds;
			return conn;
		}

		private void ReceiveConnack(){
			PacketBase packet = Transport.Read ();
			this.LastRead = Environment.TickCount;

			ConnackPacket connack = packet as ConnackPacket;

			if (packet == null){
				throw new MqttProtocolException(String.Format("First received message should be Connack, but {0} received instead", packet.GetType().Name));
			}

			if (connack.ReturnCode != ConnackReturnCode.Accepted){
				throw new MqttConnectException("The connection was not accepted", connack.ReturnCode);
			}

			this.IsSessionPresent = connack.SessionPresent;

		}

		private void Send(PacketBase packet){
			if(Transport.IsClosed){
				throw new MqttClientException ("Tried to send packet while closed");
			}
			Transport.Write (packet);
			LastWrite = Environment.TickCount;

		}

		public ushort GetNextPacketId()
		{
			ushort x = Persistence.LastOutgoingPacketId;
			if (x == Packet.MaxPacketId)
			{
				Persistence.LastOutgoingPacketId = 1;
				return 1;
			}
			else
			{
				x += 1;
				Persistence.LastOutgoingPacketId = x;
				return x;
			}
		}


		private void ReceivePacket()
		{
			PacketBase packet = Transport.Read();
			LastRead = Environment.TickCount;

			HandleReceivedPacket(packet);
		}

		void HandleReceivedPacket(PacketBase packet)
		{
			switch (packet.PacketType)
			{
			case PublishPacket.PacketTypeCode:
				OnPublishReceived(packet as PublishPacket);
				break;
			case PubackPacket.PacketTypeCode:
				OnPubackReceived(packet as PubackPacket);
				break;
			case PubrecPacket.PacketTypeCode:
				OnPubrecReceived(packet as PubrecPacket);
				break;
			case PubrelPacket.PacketTypeCode:
				OnPubrelReceived(packet as PubrelPacket);
				break;
			case PubcompPacket.PacketTypeCode:
				OnPubcompReceived(packet as PubcompPacket);
				break;
			case SubackPacket.PacketTypeCode:
				OnSubackReceived(packet as SubackPacket);
				break;
			case UnsubackPacket.PacketTypeCode:
				OnUnsubackReceived(packet as UnsubackPacket);
				break;
			case PingrespPacket.PacketTypeCode:
				break;
			default:
				throw new MqttProtocolException(String.Format("Cannot receive message of type {0}", packet.GetType().Name));
			}
		}


		// -- incoming publish events --

		void OnPublishReceived(PublishPacket packet)
		{
			//WriteToLog (Encoding.UTF8.GetString(packet.Message));
			//WriteToLog (packet.Topic);

			string pv = Encoding.UTF8.GetString (packet.Message);
			Regex reg = new Regex(@"^[-]?\d+[,]?\d+$");
			Regex reg2 = new Regex(@"^[-\d]+$");

			if (reg.IsMatch(pv)){

				int tagInd = 0;
				foreach (KPTag kpt in KPTags){
					if (kpt.Name == packet.Topic){
						SetCurData (tagInd, Convert.ToDouble (pv), 1);
						WriteToLog (kpt.CnlNum.ToString ());
					}
					tagInd++;
				}

			} else {
				if (reg2.IsMatch(pv)){
					int tagInd = 0;
					foreach (KPTag kpt in KPTags){
						if (kpt.Name == packet.Topic){
							SetCurData (tagInd, Convert.ToDouble (pv), 1);
							WriteToLog (kpt.CnlNum.ToString ());
						}
						tagInd++;
					}
				}
			}

			if (packet.QosLevel == MqttQos.ExactlyOnce)
			{
				OnQos2PublishReceived(packet);
			}
			else
			{

				if (packet.QosLevel == MqttQos.AtLeastOnce)
				{
					Send(new PubackPacket() { PacketId = packet.PacketId });
				}
			}
		}

		void OnQos2PublishReceived(PublishPacket packet)
		{
			if (!Persistence.IsIncomingFlowRegistered(packet.PacketId))
			{

				// Register the incoming packetId, so duplicate messages can be filtered.
				// This is done after "ProcessIncomingPublish" because we can't assume the
				// mesage was received in the case that method throws an exception.
				Persistence.RegisterIncomingFlow(packet.PacketId);

				// the ideal would be to run `PubishReceived` and `Persistence.RegisterIncomingFlow`
				// in a single transaction (either both or neither succeeds).
			}

			Send(new PubrecPacket() { PacketId = packet.PacketId });
		}

		void OnPubrelReceived(PubrelPacket packet)
		{

			Persistence.ReleaseIncomingFlow(packet.PacketId);
			Send(new PubcompPacket() { PacketId = packet.PacketId });
		}


		// -- outgoing publish events --

		void OnPubackReceived(PubackPacket packet)
		{
			Persistence.SetOutgoingFlowCompleted(packet.PacketId);
			this.IsPublishing = false;
		}

		void OnPubrecReceived(PubrecPacket packet)
		{
			Persistence.SetOutgoingFlowReceived(packet.PacketId);
			Send(new PubrelPacket() { PacketId = packet.PacketId });
		}

		void OnPubcompReceived(PubcompPacket packet)
		{
			Persistence.SetOutgoingFlowCompleted(packet.PacketId);
			this.IsPublishing = false;
		}


		// -- subscription events --

		void OnSubackReceived(SubackPacket packet)
		{

		}

		void OnUnsubackReceived(UnsubackPacket packet)
		{

		}

		public override void Session ()
		{
			base.Session ();
			/*
			this.Publish(new PublishPacket(){
				QosLevel = MqttQos.AtMostOnce,
				Topic = "/mesparam1",
				Message = Encoding.UTF8.GetBytes("235")
			});
			*/

			/*
			if (Transport.Poll(2000)){
				ReceivePacket ();
			}
			*/
			ReceivePacket ();
			Thread.Sleep (ReqParams.Delay);
			Send (new PingreqPacket ());
		}

		public override void OnAddedToCommLine ()
		{
			List<TagGroup> tagGroups = new List<TagGroup> ();
			TagGroup tagGroup = new TagGroup ("GroupMQTT");

			XmlDocument xmlDoc = new XmlDocument ();
			string filename = ReqParams.CmdLine.Trim ();

			xmlDoc.Load (AppDirs.ConfigDir + filename);
			XmlNode elemGroupsNode = xmlDoc.DocumentElement.SelectSingleNode("ElemGroup");
			XmlNode MQTTSettings = xmlDoc.DocumentElement.SelectSingleNode("MqttParams");

			SubscribePacket sp = new SubscribePacket ();
			int i = 0;
			sp.Topics = new string[elemGroupsNode.ChildNodes.Count];
			sp.QosLevels = new MqttQos[elemGroupsNode.ChildNodes.Count];
		
			foreach (XmlElement elemGroupElem in elemGroupsNode.ChildNodes){
				sp.Topics [i] = elemGroupElem.GetAttribute ("TopicName");
				sp.QosLevels [i] = MqttQos.AtMostOnce;
				KPTag KPt = new KPTag () {
					Signal = i + 1,
					Name= sp.Topics [i],
					CnlNum = Convert.ToInt32( elemGroupElem.GetAttribute ("NumCnl"))
				};
				tagGroup.KPTags.Add (KPt);
				i++;
			}

			tagGroups.Add (tagGroup);
			InitKPTags(tagGroups);

			connArgs = new MqttConnectionArgs ();
			connArgs.ClientId = MQTTSettings.Attributes.GetNamedItem("ClientID").Value;
			connArgs.Hostname = MQTTSettings.Attributes.GetNamedItem("Hostname").Value;
			connArgs.Port = Convert.ToInt32(MQTTSettings.Attributes.GetNamedItem("Port").Value);

			this.Persistence = new InMemoryPersistence ();
			Transport = new TcpTransport (connArgs.Hostname, connArgs.Port);
			Transport.Version = connArgs.Version;
			Transport.SetTimeouts (connArgs.ReadTimeout, connArgs.WriteTimeout);

			Send (MakeConnectMessage (connArgs));
			ReceiveConnack ();
			ResumeOutgoingFlows ();
			Subscribe (sp);
			WriteToLog ("Линия добавлена");
		}

		public override void OnCommLineTerminate ()
		{
			Send (new DisconnectPacket ());
			Transport.Close ();
			WriteToLog("Отключаемся от mqtt");
		}
	}
}

