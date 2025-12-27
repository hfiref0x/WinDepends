#include <assert.h>
#include <stdio.h>
#include <wchar.h>
#include "../src/WinDepends.Core/cmd.h"
#include "../src/WinDepends.Core/mlist.h"

void test_cmd_entry_parsing(void) {
    assert(get_command_entry(L"open") == ce_open);
    assert(get_command_entry(L"close") == ce_close);
    assert(get_command_entry(L"imports") == ce_imports);
    assert(get_command_entry(L"exports") == ce_exports);
    assert(get_command_entry(L"headers") == ce_headers);
    assert(get_command_entry(L"datadirs") == ce_datadirs);
    assert(get_command_entry(L"shutdown") == ce_shutdown);
    assert(get_command_entry(L"exit") == ce_exit);
    assert(get_command_entry(L"knowndlls") == ce_knowndlls);
    assert(get_command_entry(L"apisetresolve") == ce_apisetresolve);
    assert(get_command_entry(L"apisetmapsrc") == ce_apisetmapsrc);
    assert(get_command_entry(L"apisetnsinfo") == ce_apisetnsinfo);
    assert(get_command_entry(L"callstats") == ce_callstats);
    assert(get_command_entry(L"notacommand") == ce_unknown);
}

void test_mlist_add_and_traverse(void) {
    LIST_ENTRY head;
    PLIST_ENTRY entry;
    message_node* node;
    size_t count;
    const wchar_t* msg1 = L"msg1";
    const wchar_t* msg2 = L"some much longer message to check allocation";
    size_t len1 = wcslen(msg1);
    size_t len2 = wcslen(msg2);

    InitializeListHead(&head);

    assert(mlist_add(&head, msg1, len1) == TRUE);
    assert(mlist_add(&head, msg2, len2) == TRUE);

    count = 0;
    for (entry = head.Flink; entry != &head; entry = entry->Flink) {
        node = CONTAINING_RECORD(entry, message_node, ListEntry);
        assert(node->message != NULL);
        count++;
    }
    assert(count == 2);
}

void test_mlist_add_empty_and_failure(void) {
    LIST_ENTRY head;
    InitializeListHead(&head);

    assert(mlist_add(&head, L"", 0) == TRUE);
    assert(mlist_add(NULL, L"test", 4) == FALSE);
}

void test_mlist_traverse_send_and_cleanup(void) {
    LIST_ENTRY head;
    message_node* node;
    PLIST_ENTRY entry, next;
    InitializeListHead(&head);

    mlist_add(&head, L"abc", 3);
    mlist_add(&head, L"defgh", 5);

    entry = head.Flink;
    while (entry != &head) {
        next = entry->Flink;
        node = CONTAINING_RECORD(entry, message_node, ListEntry);
        if (!node->isStaticBuffer && node->message) {
            heap_free(GetProcessHeap(), node->message);
        }
        heap_free(GetProcessHeap(), node);
        entry = next;
    }
    InitializeListHead(&head);
}

void test_cmd_unknown_command_handler(void) {
    SOCKET fake_sock = 0;
    cmd_unknown_command(fake_sock);
}

int main(void) {
    test_cmd_entry_parsing();
    test_mlist_add_and_traverse();
    test_mlist_add_empty_and_failure();
    test_mlist_traverse_send_and_cleanup();
    test_cmd_unknown_command_handler();

    printf("All detailed WinDepends.Core tests passed.\n");
    return 0;
}