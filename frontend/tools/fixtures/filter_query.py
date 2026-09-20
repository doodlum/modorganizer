"""Temporary native filter query verification, restricted to the isolated FNV host."""
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QAbstractProxyModel, QEvent, QItemSelectionModel, QModelIndex, QTimer, Qt
from PyQt6.QtGui import QIcon, QKeyEvent
from PyQt6.QtWidgets import QApplication, QComboBox, QLineEdit, QRadioButton, QTreeView, QTreeWidget
from nexus_frontend_bridge.filter_matches import read_matches


class FilterQueryCheck(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        if self.root.name != 'mo2-fnv-host': return False
        self.organizer = organizer
        organizer.onUserInterfaceInitialized(lambda window: QTimer.singleShot(1500, lambda: self.run(window)))
        return True

    def run(self, window):
        report = self.root.parent / 'native-filter-query-controlled.json'
        try:
            tree = window.findChild(QTreeWidget, 'filters'); view = window.findChild(QTreeView, 'modList')
            search = window.findChild(QLineEdit, 'modFilterEdit')
            mode_and = window.findChild(QRadioButton, 'filtersAnd'); mode_or = window.findChild(QRadioButton, 'filtersOr')
            separators = window.findChild(QComboBox, 'filtersSeparators')
            role = int(Qt.ItemDataRole.UserRole)
            items = [tree.topLevelItem(i) for i in range(tree.topLevelItemCount())]
            assert all(x.childCount() == 0 for x in items), 'This fixture expects the installed flat native tree'
            chain=[]; model=view.model()
            while isinstance(model,QAbstractProxyModel): chain.append(model); model=model.sourceModel()
            def base(index):
                for proxy in chain:index=proxy.mapToSource(index)
                return index.row() if index.isValid() else -1
            def mapped(row):
                index=model.index(row,0)
                for proxy in reversed(chain):index=proxy.mapFromSource(index)
                return index
            selection=view.selectionModel()
            bars=[view.verticalScrollBar(),view.horizontalScrollBar(),tree.verticalScrollBar(),tree.horizontalScrollBar()]
            def capture():
                return dict(states=[int(x.data(0,role+2)) for x in items], current=tree.currentItem(),
                            text=search.text(), cursor=search.cursorPosition(), start=search.selectionStart(), length=len(search.selectedText()),
                            modes=(mode_and.isChecked(),mode_or.isChecked(),separators.currentIndex()),
                            selected=sorted(base(x) for x in selection.selectedRows()), currentMod=base(selection.currentIndex()),
                            bars=[x.value() for x in bars])
            def apply(states):
                for item,state in zip(items,states):item.setData(0,role+2,state)
                items[0].setData(0,role+2,(states[0]-1)%3);tree.setCurrentItem(items[0])
                QApplication.sendEvent(tree,QKeyEvent(QEvent.Type.KeyPress,Qt.Key.Key_Space,Qt.KeyboardModifier.NoModifier))
            original=capture()
            try:
                states=[0]*len(items)
                target=next(i for i,x in enumerate(items) if int(x.data(0,role))==10009)
                states[target]=2; apply(states)
                mode_or.setChecked(True);separators.setCurrentIndex(1)
                visible=next(mapped(i) for i in range(model.rowCount()) if mapped(i).isValid())
                text=str(visible.data(Qt.ItemDataRole.DisplayRole))[:4]
                search.setText(text);search.setSelection(len(text),-len(text))
                selection.clearSelection()
                visible=[mapped(i) for i in range(model.rowCount()) if mapped(i).isValid()]
                assert visible, 'No visible row for seeded selection'
                for index in visible[:2]:selection.select(index,QItemSelectionModel.SelectionFlag.Select|QItemSelectionModel.SelectionFlag.Rows)
                selection.setCurrentIndex(visible[0],QItemSelectionModel.SelectionFlag.NoUpdate)
                view.doItemsLayout();tree.doItemsLayout()
                for bar in bars:bar.setValue(min(10,bar.maximum()))
                before=capture()
                criteria=[{'type':int(x.data(0,role+1)),'id':int(x.data(0,role))} for x in items]
                result=read_matches(window,self.organizer,criteria)
                assert capture()==before, 'Success changed the seeded native state'
                proxy=chain[0];saved=proxy.mapFromSource;failed=[False]
                def fail_once(index):
                    if not failed[0]:failed[0]=True;raise RuntimeError('controlled mapping failure')
                    return saved(index)
                proxy.mapFromSource=fail_once
                try:
                    try:read_matches(window,self.organizer,criteria[:1])
                    except RuntimeError as error:assert str(error)=='controlled mapping failure'
                    else:raise AssertionError('Expected mapping failure')
                finally:proxy.mapFromSource=saved
                assert failed[0] and capture()==before, 'Failure did not restore native state'
                for invalid in ([criteria[0],criteria[0]],[{'type':0,'id':-99999}],[{'type':True,'id':1}]):
                    try:read_matches(window,self.organizer,invalid)
                    except ValueError:pass
                    else:raise AssertionError('Invalid criteria accepted')
                    assert capture()==before
                evidence={'passed':True,'criteria':len(result['criteria']),'elapsedMs':result['elapsedMs'],
                          'nondefaultStateRestored':True,'injectedFailureRestored':True,'invalidRequestsUnchanged':True,
                          'selectedRows':len(before['selected']),'backwardsTextSelection':before['cursor']==before['start']}
            finally:
                apply(original['states']);mode_and.setChecked(original['modes'][0]);mode_or.setChecked(original['modes'][1]);separators.setCurrentIndex(original['modes'][2])
                search.setText(original['text'])
                if original['start']>=0:
                    search.setSelection(original['start']+original['length'] if original['cursor']==original['start'] else original['start'],
                                        -original['length'] if original['cursor']==original['start'] else original['length'])
                else:search.setCursorPosition(original['cursor'])
                tree.setCurrentItem(original['current']);selection.clearSelection()
                for row in original['selected']:selection.select(mapped(row),QItemSelectionModel.SelectionFlag.Select|QItemSelectionModel.SelectionFlag.Rows)
                selection.setCurrentIndex(mapped(original['currentMod']) if original['currentMod']>=0 else QModelIndex(),QItemSelectionModel.SelectionFlag.NoUpdate)
                view.doItemsLayout();tree.doItemsLayout()
                for bar,value in zip(bars,original['bars']):bar.setValue(value)
                assert capture()==original, 'Fixture did not restore its original native state'
            report.write_text(json.dumps(evidence,indent=2))
        except Exception as error:
            report.write_text(json.dumps({'passed':False,'error':str(error)},indent=2))

    def name(self):return 'Frontend native filter query verification'
    def author(self):return 'MO2 frontend verification'
    def description(self):return 'Temporary native filter restoration check.'
    def version(self):return mobase.VersionInfo(1,0,0)
    def settings(self):return []
    def displayName(self):return self.name()
    def tooltip(self):return self.description()
    def icon(self):return QIcon()
    def display(self):pass


def createPlugin():return FilterQueryCheck()
