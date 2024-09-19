<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';
$username = $_POST['username'] ?? '';

// validate
if (empty($roomCode))
    exit;

$usersFile = 'rooms/' . $roomCode . '_users.txt';
if (file_exists($usersFile))
{
    $roomFile = 'rooms/' . $roomCode . '.txt';
    $currentUsers = file_get_contents($usersFile);
    $numUsers = count(explode('|', $currentUsers)) - 2;
    
    if ($numUsers > 0)
    {
        // remove name from user list
        file_put_contents($usersFile, str_replace($username . '|','', $currentUsers));
        
        // send farewell message
        $userJoinedMessage = "$username has left the room [$numUsers/4]\n";
        file_put_contents($roomFile, $userJoinedMessage, FILE_APPEND);
    }
    else
    {
        // room is empty; destroy it
        if (file_exists($roomFile))
            unlink($roomFile);
        unlink($usersFile);
    }
}
?>
