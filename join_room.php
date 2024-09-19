<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';
$maxUsers = 4;

// validate
if (empty($roomCode)) {
    echo json_encode(['status' => 'error', 'message' => 'Room code is empty']);
    exit;
}

// init user count
$usersFile = 'rooms/' . $roomCode . '_users.txt';
if (!file_exists($usersFile))
    file_put_contents($usersFile, '0');

// check if full
$currentUsers = (int)file_get_contents($usersFile);
if ($currentUsers >= $maxUsers)
    echo json_encode(['status' => 'error', 'message' => 'Room is full']);
else
{
    // increment user count
    ++$currentUsers;
    file_put_contents($usersFile, (string)$currentUsers);

    // send welcome message
    $roomFile = 'rooms/' . $roomCode . '.txt';
    $userJoinedMessage = "A user has joined the room [$currentUsers/4]\n";
    file_put_contents($roomFile, $userJoinedMessage, FILE_APPEND);

    echo json_encode(['status' => 'success', 'message' => 'Successfully joined room']);
}
?>
