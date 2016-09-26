KpMQTT
=============================


Драйвер коммуникатора для сбора данных по протоколу MQTT.

Для настройки линии связи используются конфигурационный файл содержание которого приведено ниже.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DevTemplate>
  <MqttParams Hostname="iot.eclipse.org" ClientID="KpMQTTmes" Port="1883" UserName="" Password=""/>
  <RapSrvCnf ServerHost="xxx.xxx.xxx.xxx" ServerPort="10000" ServerUser="ScadaComm" ServerPwd="12345"/>
  <MqttSubTopics>
  	<Topic TopicName="/myparam1" QosLevel="0" NumCnl="271"/>
  	<Topic TopicName="/myparam2" QosLevel="0" NumCnl="272"/>
  </MqttSubTopics>
  <MqttPubTopics>
	<Topic TopicName="/myparam10" QosLevel="0" NumCnl="21"/>
  </MqttPubTopics>
</DevTemplate>
```
