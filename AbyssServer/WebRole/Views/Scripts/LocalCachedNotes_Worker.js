function UpdateNotes(updatedNote, notes) {    
    if (notes == null) {
        return;
    }
    for (var i = 0; i < notes.length; i++) {
        if (notes[i].RowKey == updatedNote.RowKey) {
            notes[i] = updatedNote;
            return JSON.stringify(notes);
        }
    }
}

function DeleteNote(rowKey, notes) {    
    if (notes == null) {
        return;
    }
    for (var i = 0; i < notes.length; i++) {
        if (notes[i].RowKey == rowKey) {
            notes.splice(i, 1);
            return JSON.stringify(notes);
        }
    }
}

onmessage = function (e) {
    var updateNotes;
    if (e.data[0] == "update") {
        updateNotes = UpdateNotes(e.data[1], e.data[2]);
    }
    else if (e.data[0] == "delete") {
        updateNotes = DeleteNote(e.data[1], e.data[2]);
    }
    postMessage(updateNotes);
}