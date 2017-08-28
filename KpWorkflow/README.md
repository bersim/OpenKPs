﻿KpWorkflow
=============================

Драйвер KpWorkflow.dll является прикладной библиотекой для приложения Scada Communicator проекта RapidScada.
Этот драйвер позволяет реализовать управление последовательностью выполнения задач внутри рабочего потока (workflow). При реализации данного проекта использовались следующие библиотеки:
	- [Wexflow ] (https://github.com/aelassas/Wexflow/) 
	- [Mathparser] (https://github.com/mariuszgromada/MathParser.org-mXparser)
	
Вводная информация о работе драйвера KpWorkflow.
--------------------------------------------------------------------------------

Для понимания работы драйвера необходимо дать определение трем основным сущностям, определяющим его работу.
**Рабочий поток** - это структура описывающая задачи и последовательность их исполнения. Если последовательность исполнения не задана, то задачи описанные внутри рабочего потока выполняются последовательно.
**Задача** - это некоторый обобщенный алгоритм, для которого могут быть заданы входные и выходные параметры. В базовом виде механизм обмена данными между задачами осуществляется через файлы. В драйвере KpWorkflow добавлен механизм обмена данными через дополнительные объекты в памяти. Этот механизм задействется в задачах для которых важна скорость выполнения.
**Граф выполнения** - это структура, которая описывает последовательность выполнения задач. При описании последовательности выполнения задач могут использоваться конструкции, реализующие условные операторы **if-else**, операторы выбора **switch-case**, оператор цикла **while**, а также реакция на события задач **OnSuccess**,**OnWarning**,**OnError**.


