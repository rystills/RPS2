<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';
$username = $_POST['username'] ?? '';
$maxUsers = 4;

// validate
if (empty($roomCode)) {
    echo json_encode(['status' => 'error', 'message' => 'Room code is empty']);
    exit;
}

// init user count
$usersFile = 'rooms/' . $roomCode . '_users.txt';

// check if full
$currentUsers = file_get_contents($usersFile);
$numUsers = empty($currentUsers) ? 1 : count(explode('|', $currentUsers));

if ($numUsers > $maxUsers)
    echo json_encode(['status' => 'error', 'message' => 'Room is full']);
else
{
    // add name to user list
    file_put_contents($usersFile, $currentUsers . $username . '|');

    // send welcome message
    $roomFile = 'rooms/' . $roomCode . '.txt';
    $userJoinedMessage = "$username has joined the room [$numUsers/4]\n";
    file_put_contents($roomFile, $userJoinedMessage, FILE_APPEND);

    echo json_encode(['status' => 'success', 'message' => 'Successfully joined room']);
}
?>
