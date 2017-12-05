using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Globalization;
using Scada.Data;
using StriderMqtt;
using Scada.Client;

namespace Scada.Comm.Devices
{
	public class KpMQTTLogic : KPLogic
	{
		private IMqttTransport Transport;
		private IMqttPersistence Persistence;

		private bool IsSessionPresent { get; set; }

		private bool IsPublishing { get; set; }

		private bool InterruptLoop { get; set; }

		private MqttConnectionArgs connArgs;

		private List<MQTTPubTopic> MQTTPTs;

		private RapSrvEx RSrv;

		private SubscribePacket sp;


		public KpMQTTLogic (int number) : base (number)
		{
			ConnRequired = false;
			WorkState = WorkStates.Normal;
		}

		private void ResumeOutgoingFlows ()
		{

			foreach (OutgoingFlow flow in Persistence.GetPendingOutgoingFlows()) {
				Resume (flow);
			}
		}

		private void Resume (OutgoingFlow flow)
		{
			if (flow.Qos == MqttQos.AtLeastOnce || (flow.Qos == MqttQos.ExactlyOnce && !flow.Received)) {
				PublishPacket publish = new PublishPacket () {
					PacketId = flow.PacketId,
					QosLevel = flow.Qos,
					Topic = flow.Topic,
					Message = flow.Payload,
					DupFlag = true
				};
				Publish (publish);
			} else if (flow.Qos == MqttQos.ExactlyOnce && flow.Received) {
				Pubrel (flow.PacketId);
			}
			Persistence.LastOutgoingPacketId = flow.PacketId;
		}

		private ushort Publish (PublishPacket packet)
		{

			if (packet.QosLevel != MqttQos.AtMostOnce) {
				if (packet.PacketId == 0) {
					packet.PacketId = this.GetNextPacketId ();
				}
				Persistence.RegisterOutgoingFlow (new OutgoingFlow () {
					PacketId = packet.PacketId,
					Topic = packet.Topic,
					Qos = packet.QosLevel,
					Payload = packet.Message
				});
			}

			try {
				IsPublishing = true;
				Send (packet);
				return packet.PacketId;
			} catch {
				IsPublishing = false;
				throw;
			}
		}

		private void Pubrel (ushort packetId)
		{
			try {
				IsPublishing = true;
				Send (new PubrelPacket () { PacketId = packetId });
			} catch {
				IsPublishing = false;
				throw;
			}
		}

		private void Subscribe (SubscribePacket packet)
		{
			if (packet.PacketId == 0) {
				packet.PacketId = this.GetNextPacketId ();
			}

			Send (packet);
		}

		private void Unsubscribe (UnsubscribePacket packet)
		{
			if (packet.PacketId == 0) {
				packet.PacketId = this.GetNextPacketId ();
			}
			Send (packet);
		}


		private ConnectPacket MakeConnectMessage (MqttConnectionArgs args)
		{
			ConnectPacket conn = new ConnectPacket ();
			conn.ProtocolVersion = args.Version;

			conn.ClientId = args.ClientId;
			conn.Username = args.Username;
			conn.Password = args.Password;

			if (args.WillMessage != null) {
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

		private void ReceiveConnack ()
		{
			PacketBase packet = Transport.Read ();
			ConnackPacket connack = packet as ConnackPacket;

			if (packet == null) {
				WriteToLog (Localization.UseRussian ? String.Format ("Первый принимаемый пакет должен быть Connack, но получили {0}", packet.GetType ().Name) : 
													String.Format ("First received message should be Connack, but {0} received instead", packet.GetType ().Name));
				throw new MqttProtocolException (String.Format ("First received message should be Connack, but {0} received instead", packet.GetType ().Name));
			}

			if (connack.ReturnCode != ConnackReturnCode.Accepted) {
				WriteToLog (Localization.UseRussian ? "Соединение не разрешено брокером" : "The connection was not accepted");
				throw new MqttConnectException ("The connection was not accepted", connack.ReturnCode);
			}

			this.IsSessionPresent = connack.SessionPresent;

		}

		private void Send (PacketBase packet)
		{
			if (Transport.IsClosed) {
				WriteToLog (Localization.UseRussian ? "Попытка отправить пакет в закрытом состоянии Transport" : "Tried to send packet while closed");
				throw new MqttClientException ("Tried to send packet while closed");
			}
			Transport.Write (packet);
		}

		private ushort GetNextPacketId ()
		{
			ushort x = Persistence.LastOutgoingPacketId;
			if (x == Packet.MaxPacketId) {
				Persistence.LastOutgoingPacketId = 1;
				return 1;
			} else {
				x += 1;
				Persistence.LastOutgoingPacketId = x;
				return x;
			}
		}


		private void ReceivePacket ()
		{
			PacketBase packet = Transport.Read ();
			HandleReceivedPacket (packet);
		}

		void HandleReceivedPacket (PacketBase packet)
		{
			switch (packet.PacketType) {
			case PublishPacket.PacketTypeCode:
				OnPublishReceived (packet as PublishPacket);
				break;
			case PubackPacket.PacketTypeCode:
				OnPubackReceived (packet as PubackPacket);
				break;
			case PubrecPacket.PacketTypeCode:
				OnPubrecReceived (packet as PubrecPacket);
				break;
			case PubrelPacket.PacketTypeCode:
				OnPubrelReceived (packet as PubrelPacket);
				break;
			case PubcompPacket.PacketTypeCode:
				OnPubcompReceived (packet as PubcompPacket);
				break;
			case SubackPacket.PacketTypeCode:
				OnSubackReceived (packet as SubackPacket);
				break;
			case UnsubackPacket.PacketTypeCode:
				OnUnsubackReceived (packet as UnsubackPacket);
				break;
			case PingrespPacket.PacketTypeCode:
				break;
			default:
				WriteToLog (Localization.UseRussian ? String.Format ("Не возможно принять пакет типа {0}", packet.GetType ().Name) : String.Format ("Cannot receive message of type {0}", packet.GetType ().Name));
				throw new MqttProtocolException (String.Format ("Cannot receive message of type {0}", packet.GetType ().Name));
			}
		}


		// -- incoming publish events --

		void OnPublishReceived (PublishPacket packet)
		{
			//WriteToLog (Encoding.UTF8.GetString(packet.Message));
			//WriteToLog (packet.Topic);

			string pv = Encoding.UTF8.GetString (packet.Message);
			Regex reg = new Regex (@"^[-]?\d+[,.]?\d+$");
			Regex reg2 = new Regex (@"^[-\d]+$");

			if (reg.IsMatch (pv)) {

				int tagInd = 0;
				foreach (KPTag kpt in KPTags) {
					if (kpt.Name == packet.Topic) {
						pv = pv.Replace ('.', ',');
						SetCurData (tagInd, Convert.ToDouble (pv), 1);
						WriteToLog (kpt.CnlNum.ToString ());
					}
					tagInd++;
				}

			} else {
				if (reg2.IsMatch (pv)) {
					int tagInd = 0;
					foreach (KPTag kpt in KPTags) {
						if (kpt.Name == packet.Topic) {
							SetCurData (tagInd, Convert.ToDouble (pv), 1);
							WriteToLog (kpt.CnlNum.ToString ());
						}
						tagInd++;
					}
				}
			}

			if (packet.QosLevel == MqttQos.ExactlyOnce) {
				OnQos2PublishReceived (packet);
			} else {

				if (packet.QosLevel == MqttQos.AtLeastOnce) {
					Send (new PubackPacket () { PacketId = packet.PacketId });
				}
			}
		}

		void OnQos2PublishReceived (PublishPacket packet)
		{
			if (!Persistence.IsIncomingFlowRegistered (packet.PacketId)) {

				// Register the incoming packetId, so duplicate messages can be filtered.
				// This is done after "ProcessIncomingPublish" because we can't assume the
				// mesage was received in the case that method throws an exception.
				Persistence.RegisterIncomingFlow (packet.PacketId);

				// the ideal would be to run `PubishReceived` and `Persistence.RegisterIncomingFlow`
				// in a single transaction (either both or neither succeeds).
			}

			Send (new PubrecPacket () { PacketId = packet.PacketId });
		}

		void OnPubrelReceived (PubrelPacket packet)
		{

			Persistence.ReleaseIncomingFlow (packet.PacketId);
			Send (new PubcompPacket () { PacketId = packet.PacketId });
		}


		// -- outgoing publish events --

		void OnPubackReceived (PubackPacket packet)
		{
			Persistence.SetOutgoingFlowCompleted (packet.PacketId);
			this.IsPublishing = false;
		}

		void OnPubrecReceived (PubrecPacket packet)
		{
			Persistence.SetOutgoingFlowReceived (packet.PacketId);
			Send (new PubrelPacket () { PacketId = packet.PacketId });
		}

		void OnPubcompReceived (PubcompPacket packet)
		{
			Persistence.SetOutgoingFlowCompleted (packet.PacketId);
			this.IsPublishing = false;
		}


		// -- subscription events --

		void OnSubackReceived (SubackPacket packet)
		{

		}

		void OnUnsubackReceived (UnsubackPacket packet)
		{
			
		}

		public override void Session ()
		{
			base.Session ();


			if (WorkState == WorkStates.Error) {
				Transport.Close ();
				Transport = new TcpTransport (connArgs.Hostname, connArgs.Port);
				Transport.Version = connArgs.Version;
				Transport.SetTimeouts (connArgs.ReadTimeout, connArgs.WriteTimeout);

				Send (MakeConnectMessage (connArgs));
				ReceiveConnack ();
				ResumeOutgoingFlows ();
				if(sp.Topics.Length > 0)
					Subscribe (sp);
				WorkState = WorkStates.Normal;
				WriteToLog (Localization.UseRussian ? "Повторяем подключение с брокером MQTT" : "Retry connect in MQTT broker");
				return;
			}


			WorkState = WorkStates.Normal;

			Send (new PingreqPacket ());
			ReceivePacket ();
			MQTTPTs = RSrv.GetValues (MQTTPTs);
			NumberFormatInfo nfi = new NumberFormatInfo ();

			foreach (MQTTPubTopic mqtttp in MQTTPTs) {
				if (!mqtttp.IsPub)
					continue; 
				nfi.NumberDecimalSeparator = mqtttp.NumberDecimalSeparator;
				Publish (new PublishPacket () {
					Topic = mqtttp.TopicName,
					QosLevel = mqtttp.QosLevels,
					Message = Encoding.UTF8.GetBytes (mqtttp.Value.ToString (nfi))
				});
				mqtttp.IsPub = false;
			}

			Thread.Sleep (ReqParams.Delay);
		}


		public override void OnAddedToCommLine ()
		{
	

			
			List<TagGroup> tagGroups = new List<TagGroup> ();
			TagGroup tagGroup = new TagGroup ("GroupMQTT");

			XmlDocument xmlDoc = new XmlDocument ();
			string filename = ReqParams.CmdLine.Trim ();
			xmlDoc.Load (AppDirs.ConfigDir + filename);
			
			XmlNode MQTTSubTopics = xmlDoc.DocumentElement.SelectSingleNode ("MqttSubTopics");
			XmlNode MQTTPubTopics = xmlDoc.DocumentElement.SelectSingleNode ("MqttPubTopics");
			XmlNode RapSrvCnf = xmlDoc.DocumentElement.SelectSingleNode ("RapSrvCnf");
			XmlNode MQTTSettings = xmlDoc.DocumentElement.SelectSingleNode ("MqttParams");

			CommSettings cs = new CommSettings () {
				ServerHost = RapSrvCnf.Attributes.GetNamedItem ("ServerHost").Value,
				ServerPort = Convert.ToInt32 (RapSrvCnf.Attributes.GetNamedItem ("ServerPort").Value),
				ServerUser = RapSrvCnf.Attributes.GetNamedItem ("ServerUser").Value,
				ServerPwd = RapSrvCnf.Attributes.GetNamedItem ("ServerPwd").Value
			};

			RSrv = new RapSrvEx (cs);
			RSrv.Conn ();
			MQTTPTs = new List<MQTTPubTopic> ();



			foreach (XmlElement MqttPTCnf in MQTTPubTopics) {
				MQTTPubTopic MqttPT = new MQTTPubTopic () {
					NumCnl = Convert.ToInt32 (MqttPTCnf.GetAttribute ("NumCnl")),
					QosLevels = (MqttQos)Convert.ToByte (MqttPTCnf.GetAttribute ("QosLevel")),
					TopicName = MqttPTCnf.GetAttribute ("TopicName"),
					PubBehavior=MqttPTCnf.GetAttribute("PubBehavior"),
					NumberDecimalSeparator = MqttPTCnf.GetAttribute("NDS"),
					Value = 0
				};
				MQTTPTs.Add (MqttPT);
			}





			sp = new SubscribePacket ();
			int i = 0;

			int spCnt = MQTTSubTopics.ChildNodes.Count;


			sp.Topics = new string[MQTTSubTopics.ChildNodes.Count];
			sp.QosLevels = new MqttQos[MQTTSubTopics.ChildNodes.Count];
		
			foreach (XmlElement elemGroupElem in MQTTSubTopics.ChildNodes) {
				sp.Topics [i] = elemGroupElem.GetAttribute ("TopicName");
				sp.QosLevels [i] = (MqttQos)Convert.ToByte (elemGroupElem.GetAttribute ("QosLevel"));
				KPTag KPt = new KPTag () {
					Signal = i + 1,
					Name = sp.Topics [i],
					CnlNum = Convert.ToInt32 (elemGroupElem.GetAttribute ("NumCnl"))
				};
				tagGroup.KPTags.Add (KPt);
				i++;
			}



			tagGroups.Add (tagGroup);
			InitKPTags (tagGroups);

			connArgs = new MqttConnectionArgs ();
			connArgs.ClientId = MQTTSettings.Attributes.GetNamedItem ("ClientID").Value;
			connArgs.Hostname = MQTTSettings.Attributes.GetNamedItem ("Hostname").Value;
			connArgs.Port = Convert.ToInt32 (MQTTSettings.Attributes.GetNamedItem ("Port").Value);
			connArgs.Username = MQTTSettings.Attributes.GetNamedItem ("UserName").Value;
			connArgs.Password = MQTTSettings.Attributes.GetNamedItem ("Password").Value;

			this.Persistence = new InMemoryPersistence ();
			Transport = new TcpTransport (connArgs.Hostname, connArgs.Port);
			Transport.Version = connArgs.Version;
			Transport.SetTimeouts (connArgs.ReadTimeout, connArgs.WriteTimeout);

			Send (MakeConnectMessage (connArgs));
			ReceiveConnack ();
			ResumeOutgoingFlows ();

			if(sp.Topics.Length >0)
				Subscribe (sp);



			WriteToLog (Localization.UseRussian ? "Инициализация линии связи выполнена успешно." : "Communication line initialized successfully");



		}


		public override void OnCommLineTerminate ()
		{
			RSrv.Disconn ();
			Send (new DisconnectPacket ());
			Transport.Close ();
			WriteToLog (Localization.UseRussian ? "Отключение от MQTT брокера" : "Disconnect from MQTT broker");
			WorkState = WorkStates.Undefined;
		}
	}
}

