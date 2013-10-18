﻿Imports mangosVB.Common.BaseWriter

Public Class WS_QuestsBase
    Implements IDisposable

    'WARNING: These are used only for CharManagment
    Public ID As Integer '= 0
    Public Title As String '= ""
    Public SpecialFlags As Integer '= 0
    Public ObjectiveFlags As Integer '= 0

    Public Slot As Byte '= 0

    Public ObjectivesType() As Byte '= {0, 0, 0, 0}
    Public ObjectivesDeliver As Integer
    Public ObjectivesExplore() As Integer
    Public ObjectivesSpell() As Integer
    Public ObjectivesItem() As Integer
    Public ObjectivesItemCount() As Byte '= {0, 0, 0, 0}
    Public ObjectivesObject() As Integer
    Public ObjectivesCount() As Byte '= {0, 0, 0, 0}
    Public Explored As Boolean '= True
    Public Progress() As Byte '= {0, 0, 0, 0}
    Public ProgressItem() As Byte '= {0, 0, 0, 0}
    Public Complete As Boolean '= False
    Public Failed As Boolean '= False

    Public TimeEnd As Integer '= 0

    Public Sub New()

    End Sub

    Public Sub New(ByVal Quest As WS_QuestInfo)
        ID = 0
        Title = ""
        SpecialFlags = 0
        ObjectiveFlags = 0

        Slot = 0

        ObjectivesType = {0, 0, 0, 0}
        ObjectivesItemCount = {0, 0, 0, 0}
        ObjectivesCount = {0, 0, 0, 0}
        ObjectivesObject = {0, 0, 0, 0}
        ObjectivesExplore = {0, 0, 0, 0}
        ObjectivesSpell = {0, 0, 0, 0}
        ObjectivesItem = {0, 0, 0, 0}
        Explored = True
        Progress = {0, 0, 0, 0}
        ProgressItem = {0, 0, 0, 0}
        Complete = False
        Failed = False
        TimeEnd = 0

        'Load Spell Casts
        For bytLoop As Byte = 0 To 3
            If Quest.ObjectivesCastSpell(bytLoop) > 0 Then
                ObjectiveFlags = ObjectiveFlags Or WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_CAST
                ObjectivesType(bytLoop) = WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_CAST
                ObjectivesSpell(bytLoop) = Quest.ObjectivesCastSpell(bytLoop)
                ObjectivesObject(0) = Quest.ObjectivesKill(bytLoop)
                ObjectivesCount(0) = Quest.ObjectivesKill_Count(bytLoop)
            End If
        Next

        'Load Kills
        For bytLoop As Byte = 0 To 3
            If Quest.ObjectivesKill(bytLoop) > 0 Then
                For bytLoop2 As Byte = 0 To 3
                    If ObjectivesType(bytLoop2) = 0 Then
                        ObjectiveFlags = ObjectiveFlags Or WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_KILL
                        ObjectivesType(bytLoop2) = WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_KILL
                        ObjectivesObject(bytLoop2) = Quest.ObjectivesKill(bytLoop)
                        ObjectivesCount(bytLoop2) = Quest.ObjectivesKill_Count(bytLoop)
                        Exit For
                    End If
                Next
            End If
        Next

        'Load Items
        For bytLoop As Byte = 0 To 3
            If Quest.ObjectivesItem(bytLoop) > 0 Then
                ObjectiveFlags = ObjectiveFlags Or WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_ITEM
                ObjectivesType(bytLoop) = WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_ITEM
                ObjectivesItem(bytLoop) = Quest.ObjectivesItem(bytLoop)
                ObjectivesItemCount(bytLoop) = Quest.ObjectivesItem_Count(bytLoop)
            End If
        Next

        'Load Exploration loctions
        If (Quest.SpecialFlags And WS_QuestSystem.QuestSpecialFlag.QUEST_SPECIALFLAGS_EXPLORE) Then
            ObjectiveFlags = ObjectiveFlags Or WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_EXPLORE
            For bytLoop As Byte = 0 To 3
                ObjectivesType(bytLoop) = WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_EXPLORE
                ObjectivesExplore(bytLoop) = Quest.ObjectivesTrigger(bytLoop)
            Next
        End If
        ''TODO: Fix this below
        'If (Quest.Flags And QuestFlag.QUEST_FLAGS_EVENT) Then
        '    ObjectiveFlags = ObjectiveFlags Or QuestObjectiveFlag.QUEST_OBJECTIVE_EVENT
        '    For i = 0 To 3
        '        If ObjectivesType(i) = 0 Then
        '            ObjectivesType(i) = QuestObjectiveFlag.QUEST_OBJECTIVE_EVENT
        '            ObjectivesCount(i) = 1
        '        End If
        '    Next
        'End If

        'No objective flags are set, complete it directly
        If ObjectiveFlags = 0 Then
            For bytLoop As Byte = 0 To 3
                'Make sure these are zero
                ObjectivesObject(bytLoop) = 0
                ObjectivesCount(bytLoop) = 0
                ObjectivesExplore(bytLoop) = 0
                ObjectivesSpell(bytLoop) = 0
                ObjectivesType(bytLoop) = 0
            Next
            IsCompleted()
        End If

        Title = Quest.Title
        ID = Quest.ID
        SpecialFlags = Quest.SpecialFlags
        ObjectivesDeliver = Quest.ObjectivesDeliver
        'TODO: Fix a timer or something so that the quest really expires when it does
        If Quest.TimeLimit > 0 Then TimeEnd = GetTimestamp(Now) + Quest.TimeLimit 'The time the quest expires
    End Sub

    ''' <summary>
    ''' Updates the item count.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    Public Sub UpdateItemCount(ByRef objChar As CharacterObject)
        'DONE: Update item count at login
        For i As Byte = 0 To 3
            If ObjectivesItem(i) <> 0 Then
                ProgressItem(i) = objChar.ItemCOUNT(ObjectivesItem(i))
                Log.WriteLine(LogType.DEBUG, "ITEM COUNT UPDATED TO: {0}", ProgressItem(i))
            End If
        Next

        'DONE: If the quest doesn't require any explore than set this as completed
        If (ObjectiveFlags And WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_EXPLORE) = 0 Then Explored = True

        'DONE: Check if the quest is completed
        IsCompleted()
    End Sub

    ''' <summary>
    ''' Initializes the specified objChar.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    Public Sub Initialize(ByRef objChar As CharacterObject)
        Dim i As Byte
        If ObjectivesDeliver > 0 Then
            Dim tmpItem As New ItemObject(ObjectivesDeliver, objChar.GUID)
            If Not objChar.ItemADD(tmpItem) Then
                'DONE: Some error, unable to add item, quest is uncompletable
                tmpItem.Delete()

                Dim response As New PacketClass(OPCODES.SMSG_QUESTGIVER_QUEST_FAILED)
                response.AddInt32(ID)
                response.AddInt32(WS_QuestSystem.QuestFailedReason.FAILED_INVENTORY_FULL)
                objChar.Client.Send(response)
                response.Dispose()
                Exit Sub
            Else
                objChar.LogLootItem(tmpItem, 1, True, False)
            End If
        End If

        For i = 0 To 3
            If ObjectivesItem(i) <> 0 Then ProgressItem(i) = objChar.ItemCOUNT(ObjectivesItem(i))
        Next

        If (ObjectiveFlags And WS_QuestSystem.QuestObjectiveFlag.QUEST_OBJECTIVE_EXPLORE) Then Explored = False

        IsCompleted()
    End Sub

    ''' <summary>
    ''' Determines whether this instance is completed.
    ''' </summary>
    ''' <returns>Boolean</returns>
    Public Overridable Function IsCompleted() As Boolean
        Complete = (ObjectivesCount(0) <= Progress(0) AndAlso ObjectivesCount(1) <= Progress(1) AndAlso ObjectivesCount(2) <= Progress(2) AndAlso ObjectivesCount(3) <= Progress(3) AndAlso ObjectivesItemCount(0) <= ProgressItem(0) AndAlso ObjectivesItemCount(1) <= ProgressItem(1) AndAlso ObjectivesItemCount(2) <= ProgressItem(2) AndAlso ObjectivesItemCount(3) <= ProgressItem(3) AndAlso Explored AndAlso Failed = False)
        Return Complete
    End Function

    ''' <summary>
    ''' Gets the state.
    ''' </summary>
    ''' <param name="ForSave">if set to <c>true</c> [for save].</param>
    ''' <returns>Integer <c>1 = Complere</c><c>2 = Failed</c></returns>
    Public Overridable Function GetState(Optional ByVal ForSave As Boolean = False) As Integer
        Dim tmpState As Integer
        If Complete Then tmpState = 1
        If Failed Then tmpState = 2
        Return tmpState
    End Function

    ''' <summary>
    ''' Gets the progress.
    ''' </summary>
    ''' <param name="ForSave">if set to <c>true</c> [for save].</param>
    ''' <returns></returns>
    Public Overridable Function GetProgress(Optional ByVal ForSave As Boolean = False) As Integer
        Dim tmpProgress As Integer = 0
        If ForSave Then
            tmpProgress += CType(Progress(0), Integer)
            tmpProgress += CType(Progress(1), Integer) << 6
            tmpProgress += CType(Progress(2), Integer) << 12
            tmpProgress += CType(Progress(3), Integer) << 18
            If Explored Then tmpProgress += CType(1, Integer) << 24
            If Complete Then tmpProgress += CType(1, Integer) << 25
            If Failed Then tmpProgress += CType(1, Integer) << 26
        Else
            tmpProgress += CType(Progress(0), Integer)
            tmpProgress += CType(Progress(1), Integer) << 6
            tmpProgress += CType(Progress(2), Integer) << 12
            tmpProgress += CType(Progress(3), Integer) << 18

            If Complete Then tmpProgress += CType(1, Integer) << 24
            If Failed Then tmpProgress += CType(1, Integer) << 25
        End If
        Return tmpProgress
    End Function

    ''' <summary>
    ''' Loads the state.
    ''' </summary>
    ''' <param name="state">The state.</param>
    Public Overridable Sub LoadState(ByVal state As Integer)
        Progress(0) = state And &H3F
        Progress(1) = (state >> 6) And &H3F
        Progress(2) = (state >> 12) And &H3F
        Progress(3) = (state >> 18) And &H3F
        Explored = (((state >> 24) And &H1) = 1)
        Complete = (((state >> 25) And &H1) = 1)
        Failed = (((state >> 26) And &H1) = 1)
    End Sub

    ''' <summary>
    ''' Adds the kill.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    ''' <param name="index">The index.</param>
    ''' <param name="oGUID">The o unique identifier.</param>
    Public Sub AddKill(ByVal objChar As CharacterObject, ByVal index As Byte, ByVal oGUID As ULong)
        Progress(index) += 1
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)

        ALLQUESTS.SendQuestMessageAddKill(objChar.Client, ID, oGUID, ObjectivesObject(index), Progress(index), ObjectivesCount(index))
    End Sub

    ''' <summary>
    ''' Adds the cast.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    ''' <param name="index">The index.</param>
    ''' <param name="oGUID">The o unique identifier.</param>
    Public Sub AddCast(ByVal objChar As CharacterObject, ByVal index As Byte, ByVal oGUID As ULong)
        Progress(index) += 1
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)

        ALLQUESTS.SendQuestMessageAddKill(objChar.Client, ID, oGUID, ObjectivesObject(index), Progress(index), ObjectivesCount(index))
    End Sub

    ''' <summary>
    ''' Adds the explore.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    Public Sub AddExplore(ByVal objChar As CharacterObject)
        Explored = True
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)

        ALLQUESTS.SendQuestMessageComplete(objChar.Client, ID)
    End Sub

    ''' <summary>
    ''' Adds the emote.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    ''' <param name="index">The index.</param>
    Public Sub AddEmote(ByVal objChar As CharacterObject, ByVal index As Byte)
        Progress(index) += 1
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)

        ALLQUESTS.SendQuestMessageComplete(objChar.Client, ID)
    End Sub

    ''' <summary>
    ''' Adds the item.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    ''' <param name="index">The index.</param>
    ''' <param name="Count">The count.</param>
    Public Sub AddItem(ByVal objChar As CharacterObject, ByVal index As Byte, ByVal Count As Byte)
        If ProgressItem(index) + Count > ObjectivesItemCount(index) Then Count = ObjectivesItemCount(index) - ProgressItem(index)
        ProgressItem(index) += Count
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)

        'TODO: When item quest event is fired as it should, remove -1 here.
        Dim ItemCount As Integer = Count - 1
        ALLQUESTS.SendQuestMessageAddItem(objChar.Client, ObjectivesItem(index), ItemCount)
    End Sub

    ''' <summary>
    ''' Removes the item.
    ''' </summary>
    ''' <param name="objChar">The Character.</param>
    ''' <param name="index">The index.</param>
    ''' <param name="Count">The count.</param>
    Public Sub RemoveItem(ByVal objChar As CharacterObject, ByVal index As Byte, ByVal Count As Byte)
        If CInt(ProgressItem(index)) - CInt(Count) < 0 Then Count = ProgressItem(index)
        ProgressItem(index) -= Count
        IsCompleted()
        objChar.TalkUpdateQuest(Slot)
    End Sub

#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not Me.disposedValue Then
            ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            ' TODO: set large fields to null.
        End If
        Me.disposedValue = True
    End Sub

    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region
End Class
