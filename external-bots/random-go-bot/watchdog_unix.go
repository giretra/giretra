//go:build !windows

package main

import (
	"fmt"
	"os"
	"syscall"
	"time"
)

func startLauncherWatchdog(pid int) {
	go func() {
		for {
			time.Sleep(2 * time.Second)
			if err := syscall.Kill(pid, 0); err != nil {
				fmt.Println("Launcher process exited, shutting down.")
				os.Exit(0)
			}
		}
	}()
}
